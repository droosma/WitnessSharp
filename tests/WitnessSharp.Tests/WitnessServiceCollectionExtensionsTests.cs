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
}
