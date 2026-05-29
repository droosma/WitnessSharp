using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WitnessSharp;

/// <summary>
/// Fluent builder returned by <c>AddWitness</c> for composing OpenTelemetry tracing, metrics and logging,
/// and for accessing the underlying <see cref="IServiceCollection"/>.
/// </summary>
public interface IWitnessBuilder
{
    /// <summary>Gets the underlying service collection.</summary>
    IServiceCollection Services { get; }

    /// <summary>Escape hatch to configure the OpenTelemetry tracer provider directly.</summary>
    /// <param name="configure">A delegate that configures the <see cref="TracerProviderBuilder"/>.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    IWitnessBuilder ConfigureTracing(Action<TracerProviderBuilder> configure);

    /// <summary>Escape hatch to configure the OpenTelemetry meter provider directly.</summary>
    /// <param name="configure">A delegate that configures the <see cref="MeterProviderBuilder"/>.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    IWitnessBuilder ConfigureMetrics(Action<MeterProviderBuilder> configure);

    /// <summary>Escape hatch to configure the OpenTelemetry logging options directly.</summary>
    /// <param name="configure">A delegate that configures the <see cref="OpenTelemetryLoggerOptions"/>.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    IWitnessBuilder ConfigureLogging(Action<OpenTelemetryLoggerOptions> configure);
}
