using Azure.Monitor.OpenTelemetry.Exporter;
using WitnessSharp;

namespace Microsoft.Extensions.DependencyInjection;

public static class WitnessBuilderAzureMonitorExtensions
{
    // Stryker disable all : pass-through to Azure Monitor exporter SDK.
    public static IWitnessBuilder WithAzureMonitor(this IWitnessBuilder builder, string connectionString)
    {
        return builder.WithAzureMonitor(options => options.ConnectionString = connectionString);
    }

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
