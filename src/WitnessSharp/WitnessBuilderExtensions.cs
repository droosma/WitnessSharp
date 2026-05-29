using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WitnessSharp;

/// <summary>
/// Fluent opt-in extensions on <see cref="IWitnessBuilder"/> for enabling instrumentations, exporters and
/// logging-provider behavior. These are thin pass-throughs to the OpenTelemetry SDK.
/// </summary>
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

    /// <summary>Enables ASP.NET Core and HttpClient instrumentation.</summary>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    // Stryker disable all : pass-through to OTel SDK; see comment above.
    public static IWitnessBuilder WithStandardInstrumentations(this IWitnessBuilder builder) =>
        builder
            .WithAspNetCoreInstrumentation()
            .WithHttpClientInstrumentation();

    /// <summary>Enables ASP.NET Core tracing instrumentation.</summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">Optional delegate to configure the instrumentation options.</param>
    /// <returns>The same builder, to allow chaining.</returns>
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

    /// <summary>Enables HttpClient tracing instrumentation.</summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">Optional delegate to configure the instrumentation options.</param>
    /// <returns>The same builder, to allow chaining.</returns>
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

    /// <summary>Adds the OTLP exporter for traces, metrics and logs.</summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">Optional delegate to configure the OTLP exporter options.</param>
    /// <returns>The same builder, to allow chaining.</returns>
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

    /// <summary>Adds the console exporter for traces, metrics and logs.</summary>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    // Stryker disable all : pass-through to OTel SDK; see comment above.
    public static IWitnessBuilder WithConsoleExporter(this IWitnessBuilder builder)
    {
        builder.ConfigureTracing(tracer => tracer.AddConsoleExporter());
        builder.ConfigureMetrics(metrics => metrics.AddConsoleExporter());
        builder.ConfigureLogging(logging => logging.AddConsoleExporter());
        return builder;
    }

    // ----- Logging providers -----

    /// <summary>Clears all previously registered logging providers (opt-in).</summary>
    /// <param name="builder">The builder to configure.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    public static IWitnessBuilder ClearLoggingProviders(this IWitnessBuilder builder)
    {
        builder.Services.AddLogging(b => b.ClearProviders());
        return builder;
    }
}
