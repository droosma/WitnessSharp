# WitnessSharp

[![NuGet version](https://img.shields.io/nuget/v/WitnessSharp.svg)](https://www.nuget.org/packages/WitnessSharp)
[![Build status](https://img.shields.io/github/actions/workflow/status/droosma/WitnessSharp/build.yml?branch=main)](https://github.com/droosma/WitnessSharp/actions)
[![Mutation testing](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/droosma/WitnessSharp/badges/.badges/mutation.json)](https://github.com/droosma/WitnessSharp/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/droosma/WitnessSharp)](LICENSE)

Lean .NET observability on OpenTelemetry. `IWitness<T>` gives each call site one place for logs, metrics, and traces while keeping `ILogger<T>`, `Meter`, `ActivitySource`, and OpenTelemetry exporters directly accessible. Supports `net8.0` and `net10.0`.

## 30-second quickstart

```csharp
// Program.cs
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"))
    .WithStandardInstrumentations()
    .WithOtlpExporter();

// In your service
public sealed class OrderService(IWitness<OrderService> witness)
{
    public void PlaceOrder(int orderId)
    {
        using var action = witness.StartAction("PlaceOrder");
        action.SetTag("order.id", orderId);
        // business logic
    }
}
```

`AddWitness()` binds `WitnessOptions` from the `"Witness"` section.

## Concepts

### `IWitness<T>`

`IWitness<T>` is the main injectable — it bundles `ILogger<T>`, `Meter`, and `ActivitySource` with no new abstractions over them. Most classes only need `IWitness<T>`; for witnesses created at runtime, inject `IWitnessFactory` and call `Create<T>()`.

### `WitnessedAction`

`WitnessedAction` wraps an `Activity`. Start with `witness.StartAction("Name")`, attach tags or events, and dispose when done. Outcomes default to success; call `Failed(Exception)`, `Failed(string)`, or `Cancelled()` as needed. When started from `IWitness<T>`, the action also implements `IWitness<T>`:

```csharp
public async Task<DashboardSummary> RetrieveSummaryAsync()
{
    using var action = witness.StartAction(nameof(RetrieveSummaryAsync));
    try
    {
        var summary = await _controller.RetrieveSummaryAsync();
        action.LogDashboardSummaryRetrieved(); // typed extension method
        return summary;
    }
    catch (Exception exception)
    {
        action.Failed(exception);
        throw;
    }
}
```

Use `var` to preserve the `IWitness<T>` facet and typed extension method resolution.

### Logging via extension methods

Write extension methods on `IWitness<T>` for recurring log messages, keeping templates in one place:

```csharp
public static class OrderServiceWitnessExtensions
{
    public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId) =>
        witness.Logger.LogInformation("Order {OrderId} placed", orderId);
}
```

The analyzer package spots these and suggests the `[LoggerMessage]` pattern where performance matters.

## Installation

```bash
dotnet add package WitnessSharp
dotnet add package WitnessSharp.AzureMonitor  # optional
dotnet add package WitnessSharp.Analyzers     # optional
dotnet add package WitnessSharp.Testing       # test projects
```

## Configuration reference

Configure via `IConfiguration` or options:

```csharp
// Via IConfiguration
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"));

// Via options
builder.Services.AddWitness(options =>
{
    options.ServiceName = "orders-api";
});
```

### `appsettings.json`

```json
{
  "Witness": {
    "ServiceName": "orders-api",
    "ServiceNamespace": "Contoso.Commerce",
    "ServiceVersion": "1.3.0",
    "ServiceInstanceId": "orders-api-01",
    "DeploymentEnvironment": "Production",
    "AdditionalResourceAttributes": {
      "service.owner": "checkout",
      "cloud.region": "westeurope",
      "deployment.ring": "blue"
    }
  }
}
```

### `WitnessOptions`

| Property | Description | Default |
| --- | --- | --- |
| `ServiceName` | Sets `service.name` (the service's primary identity). | Empty string |
| `ServiceNamespace` | Sets `service.namespace`. | `null` |
| `ServiceVersion` | Sets `service.version`. | `null` |
| `ServiceInstanceId` | Sets `service.instance.id`. | `Environment.MachineName` |
| `DeploymentEnvironment` | Sets `deployment.environment`. | `DOTNET_ENVIRONMENT`, then `ASPNETCORE_ENVIRONMENT` |
| `AdditionalResourceAttributes` | Extra resource attributes applied to all signals. | Empty dictionary |

### Fluent builder methods

| Method | Purpose |
| --- | --- |
| `WithStandardInstrumentations()` | ASP.NET Core and `HttpClient` tracing |
| `WithAspNetCoreInstrumentation(...)` | ASP.NET Core tracing (use overload for filtering/enrichment) |
| `WithHttpClientInstrumentation(...)` | `HttpClient` tracing |
| `WithOtlpExporter(...)` | OTLP exporters for Collector, Jaeger, Tempo, etc. |
| `WithConsoleExporter()` | Console exporters (debugging) |
| `WithAzureMonitor(...)` | Azure Monitor exporters (from `WitnessSharp.AzureMonitor`) |
| `ClearLoggingProviders()` | Clear other logging providers before OpenTelemetry |

### Escape hatches

| Method | Purpose |
| --- | --- |
| `ConfigureTracing(Action<TracerProviderBuilder>)` | Custom sources, filters, processors, samplers, or pipelines |
| `ConfigureMetrics(Action<MeterProviderBuilder>)` | Custom meters, views, readers, or exporters |
| `ConfigureLogging(Action<OpenTelemetryLoggerOptions>)` | Logging options and exporters |

Avoid registering the same instrumentation twice: if you configure it via `ConfigureTracing`, skip the matching convenience method.

## Recipes

WitnessSharp ships no hard-coded health-check or SQL filters; use the escape hatches to add your own.

<details>
<summary>Filter out health-check spans</summary>

```csharp
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"))
    .ConfigureTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = httpContext =>
                !httpContext.Request.Path.StartsWithSegments("/health") &&
                !httpContext.Request.Path.StartsWithSegments("/ready");
        });
        tracing.AddHttpClientInstrumentation();
    })
    .WithOtlpExporter();
```

</details>

<details>
<summary>Filter spans by duration (e.g., SQL slower than 100 ms)</summary>

Create a custom processor:

```csharp
public sealed class DurationFilterProcessor : BaseProcessor<Activity>
{
    private readonly BaseExporter<Activity> _exporter;
    private readonly TimeSpan _minimumDuration;

    public DurationFilterProcessor(BaseExporter<Activity> exporter, TimeSpan minimumDuration)
    {
        _exporter = exporter;
        _minimumDuration = minimumDuration;
    }

    public override void OnEnd(Activity data)
    {
        if (data.Duration >= _minimumDuration)
            _exporter.Export(new Batch<Activity>(new[] { data }, 1));
    }

    protected override bool OnForceFlush(int timeoutMilliseconds) => true;
    protected override bool OnShutdown(int timeoutMilliseconds) => true;
}
```

Register in DI:

```csharp
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"))
    .ConfigureTracing(tracing =>
    {
        tracing.AddSqlClientInstrumentation();
        tracing.AddProcessor(new DurationFilterProcessor(
            new OtlpTraceExporter(new OtlpExporterOptions { Endpoint = new Uri("http://localhost:4317") }),
            TimeSpan.FromMilliseconds(100)));
    })
    .ConfigureMetrics(metrics => metrics.AddOtlpExporter())
    .ConfigureLogging(logging => logging.AddOtlpExporter());
```

Do not combine with `.WithOtlpExporter()` or traces will export twice.

</details>

<details>
<summary>Send all three signals to Azure Monitor</summary>

```csharp
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"))
    .WithStandardInstrumentations()
    .WithAzureMonitor(options =>
    {
        options.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    });
```

If `APPLICATIONINSIGHTS_CONNECTION_STRING` is already set, use `.WithAzureMonitor()` without arguments. See [Azure Monitor OpenTelemetry exporter docs](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme) for configuration options.

</details>

## Testing

`WitnessSharp.Testing` provides `TestWitness<T>`, an in-memory test double with `AssertLogged(...)`, `AssertMetricRecorded(...)`, and `AssertActivityStarted(...)` assertion helpers.

Example:

```csharp
using Microsoft.Extensions.Logging;
using WitnessSharp.Testing;

public class OrderServiceTests
{
    [Fact]
    public void PlaceOrder_emits_expected_telemetry()
    {
        using var witness = new TestWitness<OrderService>();
        var counter = witness.Meter.CreateCounter<int>("orders");

        witness.Logger.LogInformation("Placed order 42");
        counter.Add(1);

        using (witness.StartAction("PlaceOrder"))
        {
        }

        witness.AssertLogged(LogLevel.Information, "Placed order");
        witness.AssertMetricRecorded("orders");
        witness.AssertActivityStarted("PlaceOrder");
    }
}
```

## Analyzer (`WS0001`)

`WitnessSharp.Analyzers` flags templated `ILogger` calls in `IWitness<T>` extension methods and suggests the `[LoggerMessage]` source generator pattern. Configure severity via `.editorconfig`: `dotnet_diagnostic.WS0001.severity = warning`. See [LoggerMessage docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator).

## AOT support

WitnessSharp is AOT/trim-friendly. Upstream instrumentation and exporter packages may emit warnings when publishing with `PublishAot=true`.

## Package family

| Package | Purpose |
| --- | --- |
| `WitnessSharp` | Core primitives, DI registration, `IWitness<T>`, `WitnessedAction`, options, and fluent builder extensions |
| `WitnessSharp.AzureMonitor` | Azure Monitor exporter wiring via `.WithAzureMonitor()` |
| `WitnessSharp.Analyzers` | Roslyn analyzer package with `WS0001` |
| `WitnessSharp.Testing` | `TestWitness<T>` and assertion helpers for test projects |

## Contributing

Contributions welcome. Build with `dotnet build WitnessSharp.slnx`, test with `dotnet test WitnessSharp.slnx`, then open a pull request. Follow `CONTRIBUTING.md` if present.

## License

MIT. See [LICENSE](LICENSE).

## Further reading

- [OpenTelemetry for .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Azure Monitor OpenTelemetry exporter](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme)
