using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace WitnessSharp.Tests;

public class WitnessServiceCollectionExtensionsTests
{
    private sealed record Subject;

    private static ServiceProvider Build(Action<WitnessOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWitness(configure ?? (_ => { }));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddWitness_returns_a_WitnessBuilder()
    {
        var services = new ServiceCollection();

        var builder = services.AddWitness(_ => { });

        Assert.IsAssignableFrom<IWitnessBuilder>(builder);
    }

    [Fact]
    public void Builder_Services_is_the_passed_ServiceCollection()
    {
        var services = new ServiceCollection();

        var builder = services.AddWitness(_ => { });

        Assert.Same(services, builder.Services);
    }

    [Fact]
    public void Resolves_typed_IWitness()
    {
        using var sp = Build();

        var witness = sp.GetRequiredService<IWitness<Subject>>();

        Assert.NotNull(witness);
    }

    [Fact]
    public void Resolves_non_generic_IWitness()
    {
        using var sp = Build();

        var witness = sp.GetRequiredService<IWitness>();

        Assert.NotNull(witness);
    }

    [Fact]
    public void Resolves_IWitnessFactory()
    {
        using var sp = Build();

        var factory = sp.GetRequiredService<IWitnessFactory>();

        Assert.NotNull(factory);
    }

    [Fact]
    public void Meter_is_named_after_ServiceName_option()
    {
        using var sp = Build(opts => opts.ServiceName = "MyService");

        var meter = sp.GetRequiredService<Meter>();

        Assert.Equal("MyService", meter.Name);
    }

    [Fact]
    public void ActivitySource_is_named_after_ServiceName_option()
    {
        using var sp = Build(opts => opts.ServiceName = "MyService");

        var source = sp.GetRequiredService<ActivitySource>();

        Assert.Equal("MyService", source.Name);
    }

    [Fact]
    public void Meter_is_registered_as_singleton()
    {
        using var sp = Build();

        var a = sp.GetRequiredService<Meter>();
        var b = sp.GetRequiredService<Meter>();

        Assert.Same(a, b);
    }

    [Fact]
    public void ActivitySource_is_registered_as_singleton()
    {
        using var sp = Build();

        var a = sp.GetRequiredService<ActivitySource>();
        var b = sp.GetRequiredService<ActivitySource>();

        Assert.Same(a, b);
    }

    [Fact]
    public void Typed_IWitness_uses_shared_Meter_and_ActivitySource()
    {
        using var sp = Build();
        var meter = sp.GetRequiredService<Meter>();
        var source = sp.GetRequiredService<ActivitySource>();

        var witness = sp.GetRequiredService<IWitness<Subject>>();

        Assert.Same(meter, witness.Meter);
        Assert.Same(source, witness.ActivitySource);
    }

    [Fact]
    public void Non_generic_IWitness_resolves_to_same_instance_as_IWitness_of_object()
    {
        using var sp = Build();

        var asGeneric = sp.GetRequiredService<IWitness<object>>();
        var asNonGeneric = sp.GetRequiredService<IWitness>();

        Assert.Same(asGeneric, asNonGeneric);
    }

    [Fact]
    public void Configure_action_populates_WitnessOptions()
    {
        using var sp = Build(opts => opts.ServiceName = "FromAction");

        var opts = sp.GetRequiredService<IOptions<WitnessOptions>>();

        Assert.Equal("FromAction", opts.Value.ServiceName);
    }

    [Fact]
    public void Resource_is_composed_from_options_for_TracerProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services
            .AddWitness(opts =>
            {
                opts.ServiceName = "integration-svc";
                opts.ServiceNamespace = "team";
                opts.ServiceVersion = "1.0.0";
                opts.ServiceInstanceId = "i-42";
            })
            // TracerProvider is only registered when WithTracing is wired up.
            .ConfigureTracing(_ => { });
        using var sp = services.BuildServiceProvider();

        var tracer = sp.GetRequiredService<OpenTelemetry.Trace.TracerProvider>();
        var attrs = ResourceProbe.ReadAttributes(tracer);

        Assert.Equal("integration-svc", attrs["service.name"]);
        Assert.Equal("team", attrs["service.namespace"]);
        Assert.Equal("1.0.0", attrs["service.version"]);
        Assert.Equal("i-42", attrs["service.instance.id"]);
    }
}

/// <summary>
/// OTel's <c>GetResource()</c> extension on <c>BaseProvider</c> is internal in
/// the 1.x package family. We need a way to inspect the resource a provider
/// was built with to verify the AddWitness pipeline end-to-end. Reflection is
/// the cheapest workaround for a test-only concern.
/// </summary>
internal static class ResourceProbe
{
    public static Dictionary<string, object> ReadAttributes(OpenTelemetry.BaseProvider provider)
    {
        var method = typeof(OpenTelemetry.Sdk).Assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static))
            .FirstOrDefault(m => m.Name == "GetResource"
                && m.GetParameters().Length == 1
                && m.GetParameters()[0].ParameterType == typeof(OpenTelemetry.BaseProvider))
            ?? throw new InvalidOperationException("GetResource(BaseProvider) not found in OpenTelemetry assembly.");
        var resource = (OpenTelemetry.Resources.Resource)method.Invoke(null, [provider])!;
        return resource.Attributes.ToDictionary(a => a.Key, a => a.Value);
    }
}
