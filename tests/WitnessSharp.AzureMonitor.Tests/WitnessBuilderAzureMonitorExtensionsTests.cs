using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WitnessSharp.AzureMonitor.Tests;

public class WitnessBuilderAzureMonitorExtensionsTests
{
    private const string _connectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";

    private static IWitnessBuilder NewBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.AddWitness(options => options.ServiceName = "test");
    }

    private static ServiceProvider BuildProvider(IWitnessBuilder builder) =>
        builder.Services.BuildServiceProvider();

    [Fact]
    public void WithAzureMonitor_no_arg_does_not_throw()
    {
        var builder = NewBuilder();

        var exception = Record.Exception(() => builder.WithAzureMonitor());

        Assert.Null(exception);
    }

    [Fact]
    public void WithAzureMonitor_no_arg_returns_builder()
    {
        var builder = NewBuilder();

        var result = builder.WithAzureMonitor();

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithAzureMonitor_no_arg_builds_all_three_providers()
    {
        var originalConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", _connectionString);

        try
        {
            var builder = NewBuilder().WithAzureMonitor();
            using var sp = BuildProvider(builder);

            Assert.NotNull(sp.GetRequiredService<TracerProvider>());
            Assert.NotNull(sp.GetRequiredService<MeterProvider>());
            Assert.NotNull(sp.GetRequiredService<LoggerProvider>());
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING", originalConnectionString);
        }
    }

    [Fact]
    public void WithAzureMonitor_connection_string_does_not_throw()
    {
        var builder = NewBuilder();

        var exception = Record.Exception(() => builder.WithAzureMonitor(_connectionString));

        Assert.Null(exception);
    }

    [Fact]
    public void WithAzureMonitor_connection_string_returns_builder()
    {
        var builder = NewBuilder();

        var result = builder.WithAzureMonitor(_connectionString);

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithAzureMonitor_configure_action_does_not_throw()
    {
        var builder = NewBuilder();

        var exception = Record.Exception(() => builder.WithAzureMonitor(options => options.ConnectionString = _connectionString));

        Assert.Null(exception);
    }

    [Fact]
    public void WithAzureMonitor_configure_action_returns_builder()
    {
        var builder = NewBuilder();

        var result = builder.WithAzureMonitor(options => options.ConnectionString = _connectionString);

        Assert.Same(builder, result);
    }

    [Fact]
    public void WithAzureMonitor_configure_action_builds_all_three_providers()
    {
        var builder = NewBuilder().WithAzureMonitor(options => options.ConnectionString = _connectionString);
        using var sp = BuildProvider(builder);

        Assert.NotNull(sp.GetRequiredService<TracerProvider>());
        Assert.NotNull(sp.GetRequiredService<MeterProvider>());
        Assert.NotNull(sp.GetRequiredService<LoggerProvider>());
    }
}
