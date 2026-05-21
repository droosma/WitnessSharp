using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WitnessSharp;

public interface IWitnessBuilder
{
    IServiceCollection Services { get; }

    IWitnessBuilder ConfigureTracing(Action<TracerProviderBuilder> configure);
    IWitnessBuilder ConfigureMetrics(Action<MeterProviderBuilder> configure);
    IWitnessBuilder ConfigureLogging(Action<OpenTelemetryLoggerOptions> configure);
}
