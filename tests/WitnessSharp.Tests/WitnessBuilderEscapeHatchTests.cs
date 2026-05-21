using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WitnessSharp.Tests;

public class WitnessBuilderEscapeHatchTests
{
    private static IWitnessBuilder NewBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.AddWitness(opts => opts.ServiceName = "x");
    }

    private static ServiceProvider BuildProvider(IWitnessBuilder builder) =>
        builder.Services.BuildServiceProvider();

    // ----- ConfigureTracing -----

    [Fact]
    public void ConfigureTracing_returns_same_builder_for_chaining()
    {
        var builder = NewBuilder();

        var returned = builder.ConfigureTracing(_ => { });

        Assert.Same(builder, returned);
    }

    [Fact]
    public void ConfigureTracing_invokes_action_when_tracer_provider_is_built()
    {
        var invoked = false;
        var builder = NewBuilder().ConfigureTracing(_ => invoked = true);
        using var sp = BuildProvider(builder);

        _ = sp.GetRequiredService<TracerProvider>();

        Assert.True(invoked);
    }

    // ----- ConfigureMetrics -----

    [Fact]
    public void ConfigureMetrics_returns_same_builder_for_chaining()
    {
        var builder = NewBuilder();

        var returned = builder.ConfigureMetrics(_ => { });

        Assert.Same(builder, returned);
    }

    [Fact]
    public void ConfigureMetrics_invokes_action_when_meter_provider_is_built()
    {
        var invoked = false;
        var builder = NewBuilder().ConfigureMetrics(_ => invoked = true);
        using var sp = BuildProvider(builder);

        _ = sp.GetRequiredService<MeterProvider>();

        Assert.True(invoked);
    }

    // ----- ConfigureLogging -----

    [Fact]
    public void ConfigureLogging_returns_same_builder_for_chaining()
    {
        var builder = NewBuilder();

        var returned = builder.ConfigureLogging(_ => { });

        Assert.Same(builder, returned);
    }

    [Fact]
    public void ConfigureLogging_invokes_action_when_logger_provider_is_built()
    {
        var invoked = false;
        var builder = NewBuilder().ConfigureLogging(_ => invoked = true);
        using var sp = BuildProvider(builder);

        _ = sp.GetRequiredService<LoggerProvider>();

        Assert.True(invoked);
    }
}
