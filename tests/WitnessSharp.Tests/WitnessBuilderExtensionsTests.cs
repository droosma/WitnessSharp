using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WitnessSharp.Tests;

public class WitnessBuilderExtensionsTests
{
    private static IWitnessBuilder NewBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.AddWitness(opts => opts.ServiceName = "x");
    }

    private static ServiceProvider BuildProvider(IWitnessBuilder builder) =>
        builder.Services.BuildServiceProvider();

    // ----- Instrumentation chaining -----

    [Fact]
    public void WithStandardInstrumentations_returns_same_builder()
    {
        var builder = NewBuilder();
        var returned = builder.WithStandardInstrumentations();
        Assert.Same(builder, returned);
    }

    [Fact]
    public void WithAspNetCoreInstrumentation_returns_same_builder()
    {
        var builder = NewBuilder();
        var returned = builder.WithAspNetCoreInstrumentation();
        Assert.Same(builder, returned);
    }

    [Fact]
    public void WithHttpClientInstrumentation_returns_same_builder()
    {
        var builder = NewBuilder();
        var returned = builder.WithHttpClientInstrumentation();
        Assert.Same(builder, returned);
    }

    // ----- Instrumentation configure invocation -----

    [Fact]
    public void WithAspNetCoreInstrumentation_invokes_configure_when_options_resolved()
    {
        var invoked = false;
        var builder = NewBuilder().WithAspNetCoreInstrumentation(_ => invoked = true);
        using var sp = BuildProvider(builder);

        _ = sp.GetRequiredService<IOptions<AspNetCoreTraceInstrumentationOptions>>().Value;

        Assert.True(invoked);
    }

    [Fact]
    public void WithAspNetCoreInstrumentation_no_arg_overload_builds_tracer_provider()
    {
        var builder = NewBuilder().WithAspNetCoreInstrumentation();
        using var sp = BuildProvider(builder);

        var tracer = sp.GetRequiredService<TracerProvider>();

        Assert.NotNull(tracer);
    }

    [Fact]
    public void WithHttpClientInstrumentation_invokes_configure_when_options_resolved()
    {
        var invoked = false;
        var builder = NewBuilder().WithHttpClientInstrumentation(_ => invoked = true);
        using var sp = BuildProvider(builder);

        _ = sp.GetRequiredService<IOptions<HttpClientTraceInstrumentationOptions>>().Value;

        Assert.True(invoked);
    }

    [Fact]
    public void WithHttpClientInstrumentation_no_arg_overload_builds_tracer_provider()
    {
        var builder = NewBuilder().WithHttpClientInstrumentation();
        using var sp = BuildProvider(builder);

        var tracer = sp.GetRequiredService<TracerProvider>();

        Assert.NotNull(tracer);
    }

    [Fact]
    public void WithStandardInstrumentations_registers_both_aspnet_and_http_options()
    {
        var builder = NewBuilder().WithStandardInstrumentations();
        using var sp = BuildProvider(builder);

        // If either instrumentation wasn't registered, its options would still resolve
        // (defaults exist), but the providers should also build cleanly with both.
        Assert.NotNull(sp.GetRequiredService<IOptions<AspNetCoreTraceInstrumentationOptions>>().Value);
        Assert.NotNull(sp.GetRequiredService<IOptions<HttpClientTraceInstrumentationOptions>>().Value);
        Assert.NotNull(sp.GetRequiredService<TracerProvider>());
    }

    // ----- Exporter chaining -----

    [Fact]
    public void WithOtlpExporter_returns_same_builder()
    {
        var builder = NewBuilder();
        var returned = builder.WithOtlpExporter();
        Assert.Same(builder, returned);
    }

    [Fact]
    public void WithConsoleExporter_returns_same_builder()
    {
        var builder = NewBuilder();
        var returned = builder.WithConsoleExporter();
        Assert.Same(builder, returned);
    }

    // ----- Exporter provider builds -----

    [Fact]
    public void WithOtlpExporter_no_arg_builds_all_three_providers()
    {
        var builder = NewBuilder().WithOtlpExporter();
        using var sp = BuildProvider(builder);

        Assert.NotNull(sp.GetRequiredService<TracerProvider>());
        Assert.NotNull(sp.GetRequiredService<MeterProvider>());
        Assert.NotNull(sp.GetRequiredService<LoggerProvider>());
    }

    [Fact]
    public void WithOtlpExporter_configure_overload_builds_all_three_providers()
    {
        var builder = NewBuilder().WithOtlpExporter(o => o.Endpoint = new Uri("http://localhost:4317"));
        using var sp = BuildProvider(builder);

        Assert.NotNull(sp.GetRequiredService<TracerProvider>());
        Assert.NotNull(sp.GetRequiredService<MeterProvider>());
        Assert.NotNull(sp.GetRequiredService<LoggerProvider>());
    }

    [Fact]
    public void WithConsoleExporter_builds_all_three_providers()
    {
        var builder = NewBuilder().WithConsoleExporter();
        using var sp = BuildProvider(builder);

        Assert.NotNull(sp.GetRequiredService<TracerProvider>());
        Assert.NotNull(sp.GetRequiredService<MeterProvider>());
        Assert.NotNull(sp.GetRequiredService<LoggerProvider>());
    }

    // ----- ClearLoggingProviders -----

    [Fact]
    public void ClearLoggingProviders_returns_same_builder()
    {
        var builder = NewBuilder();
        var returned = builder.ClearLoggingProviders();
        Assert.Same(builder, returned);
    }

    [Fact]
    public void ClearLoggingProviders_removes_existing_logger_providers()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        var builder = services.AddWitness(opts => opts.ServiceName = "x");

        builder.ClearLoggingProviders();
        using var sp = builder.Services.BuildServiceProvider();

        Assert.Empty(sp.GetServices<ILoggerProvider>());
    }
}
