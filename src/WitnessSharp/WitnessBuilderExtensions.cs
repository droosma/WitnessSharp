using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WitnessSharp;

public static class WitnessBuilderExtensions
{
    // ----- Instrumentation -----
    //
    // The instrumentation and exporter methods below are thin pass-throughs to OTel SDK
    // extension methods. Verifying that the pass-through call actually happens requires
    // running the instrumented framework end-to-end (ASP.NET Core pipeline, HttpClient
    // requests, OTLP collector, console capture) — out of scope for our unit test suite.
    // Per PLAN, the Analyzer package is similarly exempted from Stryker because the
    // Roslyn harness validates behavior; here we lean on OTel's own test suite for the
    // pass-through guarantee.

    // Stryker disable all : pass-through to OTel SDK; see comment above.
    public static IWitnessBuilder WithStandardInstrumentations(this IWitnessBuilder builder) =>
        builder
            .WithAspNetCoreInstrumentation()
            .WithHttpClientInstrumentation();

    // Stryker disable all : pass-through to OTel SDK; see comment above.
    public static IWitnessBuilder WithAspNetCoreInstrumentation(
        this IWitnessBuilder builder,
        Action<AspNetCoreTraceInstrumentationOptions>? configure = null) =>
        builder.ConfigureTracing(tracer =>
        {
            if (configure is null)
            {
                tracer.AddAspNetCoreInstrumentation();
            }
            else
            {
                tracer.AddAspNetCoreInstrumentation(configure);
            }
        });

    // Stryker disable all : pass-through to OTel SDK; see comment above.
    public static IWitnessBuilder WithHttpClientInstrumentation(
        this IWitnessBuilder builder,
        Action<HttpClientTraceInstrumentationOptions>? configure = null) =>
        builder.ConfigureTracing(tracer =>
        {
            if (configure is null)
            {
                tracer.AddHttpClientInstrumentation();
            }
            else
            {
                tracer.AddHttpClientInstrumentation(configure);
            }
        });

    // ----- Exporters -----

    // Stryker disable all : pass-through to OTel SDK; see comment above.
    public static IWitnessBuilder WithOtlpExporter(
        this IWitnessBuilder builder,
        Action<OtlpExporterOptions>? configure = null)
    {
        builder.ConfigureTracing(tracer =>
        {
            if (configure is null)
            {
                tracer.AddOtlpExporter();
            }
            else
            {
                tracer.AddOtlpExporter(configure);
            }
        });
        builder.ConfigureMetrics(metrics =>
        {
            if (configure is null)
            {
                metrics.AddOtlpExporter();
            }
            else
            {
                metrics.AddOtlpExporter(configure);
            }
        });
        builder.ConfigureLogging(logging =>
        {
            if (configure is null)
            {
                logging.AddOtlpExporter();
            }
            else
            {
                logging.AddOtlpExporter(configure);
            }
        });
        return builder;
    }

    // Stryker disable all : pass-through to OTel SDK; see comment above.
    public static IWitnessBuilder WithConsoleExporter(this IWitnessBuilder builder)
    {
        builder.ConfigureTracing(tracer => tracer.AddConsoleExporter());
        builder.ConfigureMetrics(metrics => metrics.AddConsoleExporter());
        builder.ConfigureLogging(logging => logging.AddConsoleExporter());
        return builder;
    }

    // ----- Logging providers -----

    public static IWitnessBuilder ClearLoggingProviders(this IWitnessBuilder builder)
    {
        builder.Services.AddLogging(b => b.ClearProviders());
        return builder;
    }
}
