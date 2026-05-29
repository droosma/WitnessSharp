using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WitnessSharp;

/// <summary>The default <see cref="IWitnessBuilder"/> implementation.</summary>
public sealed class WitnessBuilder : IWitnessBuilder
{
    /// <inheritdoc/>
    public IServiceCollection Services { get; }

    /// <summary>Initializes a new instance of the <see cref="WitnessBuilder"/> class.</summary>
    /// <param name="services">The underlying service collection.</param>
    public WitnessBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <inheritdoc/>
    public IWitnessBuilder ConfigureTracing(Action<TracerProviderBuilder> configure)
    {
        Services.AddOpenTelemetry().WithTracing(configure);
        return this;
    }

    /// <inheritdoc/>
    public IWitnessBuilder ConfigureMetrics(Action<MeterProviderBuilder> configure)
    {
        Services.AddOpenTelemetry().WithMetrics(configure);
        return this;
    }

    /// <inheritdoc/>
    public IWitnessBuilder ConfigureLogging(Action<OpenTelemetryLoggerOptions> configure)
    {
        Services.AddOpenTelemetry().WithLogging(configureBuilder: null, configureOptions: configure);
        return this;
    }
}
