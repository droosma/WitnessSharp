using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WitnessSharp;

public sealed class WitnessBuilder : IWitnessBuilder
{
    public IServiceCollection Services { get; }

    public WitnessBuilder(IServiceCollection services)
    {
        Services = services;
    }

    public IWitnessBuilder ConfigureTracing(Action<TracerProviderBuilder> configure)
    {
        Services.AddOpenTelemetry().WithTracing(configure);
        return this;
    }

    public IWitnessBuilder ConfigureMetrics(Action<MeterProviderBuilder> configure)
    {
        Services.AddOpenTelemetry().WithMetrics(configure);
        return this;
    }

    public IWitnessBuilder ConfigureLogging(Action<OpenTelemetryLoggerOptions> configure)
    {
        Services.AddOpenTelemetry().WithLogging(configureBuilder: null, configureOptions: configure);
        return this;
    }
}
