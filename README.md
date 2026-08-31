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

`WitnessedAction` wraps an `Activity`. Start one with `witness.StartAction("Name")`, attach tags or events, and dispose when the operation ends. Outcomes default to success; call `Failed(Exception)`, `Failed(string)`, or `Cancelled()` as needed. `Finish()` stops recording early without disposing the wrapper.

When started from a typed `IWitness<T>`, the action also implements `IWitness<T>`, so extension methods resolve directly:

```csharp
public async Task<DashboardSummary> RetrieveSummaryAsync()
{
    using var action = witness.StartAction(nameof(RetrieveSummaryAsync));
    try
    {
        var summary = await _controller.RetrieveSummaryAsync();
        action.LogDashboardSummaryRetrieved(); // same extension you'd call on the witness
        return summary;
    }
    catch (Exception exception)
    {
        action.Failed(exception);
        throw;
    }
}
```

Use `var` (not an explicit `WitnessedAction` type) so the action keeps its `IWitness<T>` facet and the typed extension methods resolve.

### Logging via extension methods

WitnessSharp leans toward extension methods on `IWitness<T>` for recurring log messages. That keeps message templates in one place and keeps call sites small.

```csharp
public static class OrderServiceWitnessExtensions
{
    public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId) =>
        witness.Logger.LogInformation("Order {OrderId} placed", orderId);
}
```

The optional analyzer package spots these patterns and nudges you toward `LoggerMessage` where it pays off.

## Installation

```bash
dotnet add package WitnessSharp
dotnet add package WitnessSharp.AzureMonitor  # optional
dotnet add package WitnessSharp.Analyzers     # optional
dotnet add package WitnessSharp.Testing       # test projects
```

## Configuration reference

You can configure WitnessSharp with either overload:

```csharp
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"));

// or
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

| Method | What it does | Notes |
| --- | --- | --- |
| `WithStandardInstrumentations()` | Adds ASP.NET Core and `HttpClient` tracing instrumentation. | |
| `WithAspNetCoreInstrumentation(...)` | Adds ASP.NET Core tracing instrumentation. | Use the overload when you need request filtering or enrichment. |
| `WithHttpClientInstrumentation(...)` | Adds `HttpClient` tracing instrumentation. | |
| `WithOtlpExporter(...)` | Adds OTLP exporters for traces, metrics, and logs. | Good fit for OpenTelemetry Collector, Jaeger, Tempo, and similar backends. |
| `WithConsoleExporter()` | Adds console exporters for traces, metrics, and logs. | Handy for local debugging. |
| `WithAzureMonitor(...)` | Adds Azure Monitor exporters for traces, metrics, and logs. | Comes from `WitnessSharp.AzureMonitor`. |
| `ClearLoggingProviders()` | Clears existing `Microsoft.Extensions.Logging` providers before OpenTelemetry logging is added. | Opt in only if you want OTel to be the only logging provider. |

### Escape hatches

| Method | Use it for |
| --- | --- |
| `ConfigureTracing(Action<TracerProviderBuilder>)` | Custom sources, filters, processors, samplers, or exporter pipelines |
| `ConfigureMetrics(Action<MeterProviderBuilder>)` | Custom meters, views, readers, or exporters |
| `ConfigureLogging(Action<OpenTelemetryLoggerOptions>)` | OpenTelemetry logging options and exporters |

If you configure an instrumentation manually through `ConfigureTracing`, skip the matching convenience method to avoid registering the same instrumentation twice.

## Recipes

WitnessSharp ships no hard-coded health-check or SQL filters; use the escape hatches to add your own.

<details>
<summary>Filter out health-check spans</summary>

Use `ConfigureTracing()` when you need to own the ASP.NET Core instrumentation options.

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
<summary>Filter fast SQL spans with a custom processor</summary>

Duration-based SQL filtering is app-specific. This example keeps SQL spans ≥ 100 ms and exports all others normally. It assumes the SQL client instrumentation package is installed.

```csharp
using System.Diagnostics;
using System.Linq;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

public sealed class MinimumDurationSqlProcessor : BaseProcessor<Activity>
{
    private readonly BatchActivityExportProcessor _inner;
    private readonly TimeSpan _minimumDuration;

    public MinimumDurationSqlProcessor(BaseExporter<Activity> exporter, TimeSpan minimumDuration)
    {
        _inner = new BatchActivityExportProcessor(exporter);
        _minimumDuration = minimumDuration;
    }

    public override void OnEnd(Activity data)
    {
        var isSqlSpan = data.Kind == ActivityKind.Client &&
            data.Tags.Any(tag => tag.Key == "db.system");

        if (!isSqlSpan || data.Duration >= _minimumDuration)
        {
            _inner.OnEnd(data);
        }
    }

    protected override bool OnForceFlush(int timeoutMilliseconds) =>
        _inner.ForceFlush(timeoutMilliseconds);

    protected override bool OnShutdown(int timeoutMilliseconds) =>
        _inner.Shutdown(timeoutMilliseconds);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}
```

```csharp
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"))
    .ConfigureTracing(tracing =>
    {
        tracing.AddSqlClientInstrumentation();
        tracing.AddProcessor(new MinimumDurationSqlProcessor(
            new OtlpTraceExporter(new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317")
            }),
            TimeSpan.FromMilliseconds(100)));
    })
    .ConfigureMetrics(metrics => metrics.AddOtlpExporter())
    .ConfigureLogging(logging => logging.AddOtlpExporter());
```

Do not combine this trace setup with `.WithOtlpExporter()`, or you will export traces twice.

</details>

<details>
<summary>Send all three signals to Azure Monitor</summary>

Install `WitnessSharp.AzureMonitor`, then add the Azure Monitor exporters with one call.

```csharp
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"))
    .WithStandardInstrumentations()
    .WithAzureMonitor(options =>
    {
        options.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    });
```

If your environment already sets `APPLICATIONINSIGHTS_CONNECTION_STRING`, the parameterless `.WithAzureMonitor()` overload also works.

See the [Azure Monitor OpenTelemetry exporter docs](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme) for Azure-specific options and guidance.

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

`WitnessSharp.Analyzers` (optional) adds `WS0001`, which flags templated `ILogger` calls inside `IWitness<T>` extension methods (as shown above) and suggests the `[LoggerMessage]` source generator. A code fix handles the rewrite. Configure severity in `.editorconfig`: `dotnet_diagnostic.WS0001.severity = warning`. See the [`LoggerMessage` docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator) for background.

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
