using Azure.Monitor.OpenTelemetry.Exporter;
using WitnessSharp;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Extensions on <see cref="IWitnessBuilder"/> for wiring the Azure Monitor exporter.</summary>
public static class WitnessBuilderAzureMonitorExtensions
{
    /// <summary>Adds the Azure Monitor exporter for traces, metrics and logs using a connection string.</summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="connectionString">The Azure Monitor connection string.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    // Stryker disable all : pass-through to Azure Monitor exporter SDK.
    public static IWitnessBuilder WithAzureMonitor(this IWitnessBuilder builder, string connectionString)
    {
        return builder.WithAzureMonitor(options => options.ConnectionString = connectionString);
    }

    /// <summary>Adds the Azure Monitor exporter for traces, metrics and logs.</summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">Optional delegate to configure the Azure Monitor exporter options.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    // Stryker disable all : pass-through to Azure Monitor exporter SDK.
    public static IWitnessBuilder WithAzureMonitor(
        this IWitnessBuilder builder,
        Action<AzureMonitorExporterOptions>? configure = null)
    {
        builder.ConfigureTracing(tracer =>
        {
            if (configure is null)
            {
                tracer.AddAzureMonitorTraceExporter();
            }
            else
            {
                tracer.AddAzureMonitorTraceExporter(configure);
            }
        });
        builder.ConfigureMetrics(metrics =>
        {
            if (configure is null)
            {
                metrics.AddAzureMonitorMetricExporter();
            }
            else
            {
                metrics.AddAzureMonitorMetricExporter(configure);
            }
        });
        builder.ConfigureLogging(logging =>
        {
            if (configure is null)
            {
                logging.AddAzureMonitorLogExporter();
            }
            else
            {
                logging.AddAzureMonitorLogExporter(configure);
            }
        });
        return builder;
    }
}
