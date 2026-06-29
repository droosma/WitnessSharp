# WitnessSharp

[![NuGet version](https://img.shields.io/nuget/v/WitnessSharp.svg)](https://www.nuget.org/packages/WitnessSharp)
[![Build status](https://img.shields.io/github/actions/workflow/status/droosma/WitnessSharp/build.yml?branch=main)](https://github.com/droosma/WitnessSharp/actions)
[![Mutation testing](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/droosma/WitnessSharp/badges/.badges/mutation.json)](https://github.com/droosma/WitnessSharp/actions/workflows/build.yml)
[![License](https://img.shields.io/github/license/droosma/WitnessSharp)](LICENSE)

Lean .NET observability on OpenTelemetry. `IWitness<T>` gives each call site one place for logs, metrics, and traces.

WitnessSharp keeps the underlying .NET types visible. You still work with `ILogger<T>`, `Meter`, `ActivitySource`, configuration binding, and OpenTelemetry exporters. The package just gives them a clean shape and a small bootstrap API.

Supports `net8.0` and `net10.0`.

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

`AddWitness()` binds `WitnessOptions` from the `"Witness"` section. Registration without any `.With*()` calls is valid if you only want the core primitives.

## Concepts

### `IWitness<T>`

`IWitness<T>` is the main thing you inject. It bundles:

- `ILogger<T>` for logs
- `Meter` for metrics
- `ActivitySource` for traces

That shape keeps constructors short and keeps related observability tools together. It also avoids inventing new logging or metrics abstractions. If you already know the built-in .NET types, you already know most of WitnessSharp.

Most classes only need `IWitness<T>`. If you need a typed witness for a type discovered at runtime, inject `IWitnessFactory` and call `Create<T>()`.

### `WitnessedAction`

`WitnessedAction` is a small wrapper around an `Activity`. Start one with `witness.StartAction("Name")`, attach tags or events, and dispose it when the operation ends.

Outcomes are explicit:

- success is the default
- `Failed(Exception)` or `Failed(string)` marks the action as a failure
- `Cancelled()` marks it as cancelled

`Dispose()` sets the final activity status and closes the activity. `Finish()` is also available when you need to stop early without disposing the wrapper yet.

When started from a typed `IWitness<T>`, the action is itself an `IWitness<T>`. That means the same `IWitness<T>` extension methods you call on a witness (such as logging helpers) can be called directly on the action, keeping a single operation's call site consistent:

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

### Design philosophy

- Lean defaults. Nothing is enabled unless you opt in.
- Fluent setup. Start with `AddWitness()`, then add instrumentations and exporters you actually want.
- Native .NET first. WitnessSharp does not hide `ILogger`, `Meter`, `ActivitySource`, `IConfiguration`, or OpenTelemetry builders.
- One injectable per call site. Logs, metrics, and traces stay together.

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
| `ServiceName` | Sets `service.name`. This is the main identity of your service. | Empty string. Set this in real apps. |
| `ServiceNamespace` | Sets `service.namespace`. Useful when several services share the same base name. | `null` |
| `ServiceVersion` | Sets `service.version`. | `null` |
| `ServiceInstanceId` | Sets `service.instance.id`. | `Environment.MachineName` |
| `DeploymentEnvironment` | Sets `deployment.environment`. | `DOTNET_ENVIRONMENT`, then `ASPNETCORE_ENVIRONMENT` |
| `AdditionalResourceAttributes` | Adds any extra resource attributes you want on logs, metrics, and traces. | Empty dictionary |

### Fluent builder methods

Registration by itself is valid. Add builder methods when you want instrumentations or exporters.

| Method | What it does | Notes |
| --- | --- | --- |
| `WithStandardInstrumentations()` | Adds ASP.NET Core and `HttpClient` tracing instrumentation. | Good default for web apps. |
| `WithAspNetCoreInstrumentation(...)` | Adds ASP.NET Core tracing instrumentation. | Use the overload when you need request filtering or enrichment. |
| `WithHttpClientInstrumentation(...)` | Adds `HttpClient` tracing instrumentation. | Useful for outbound calls from services or APIs. |
| `WithOtlpExporter(...)` | Adds OTLP exporters for traces, metrics, and logs. | Good fit for OpenTelemetry Collector, Jaeger, Tempo, and similar backends. |
| `WithConsoleExporter()` | Adds console exporters for traces, metrics, and logs. | Handy for local debugging. |
| `WithAzureMonitor(...)` | Adds Azure Monitor exporters for traces, metrics, and logs. | Comes from `WitnessSharp.AzureMonitor`. |
| `ClearLoggingProviders()` | Clears existing `Microsoft.Extensions.Logging` providers before OpenTelemetry logging is added. | Opt in only if you want OTel to be the only logging provider. |

### Escape hatches

Use the escape hatches when the built-in convenience methods are not enough:

| Method | Use it for |
| --- | --- |
| `ConfigureTracing(Action<TracerProviderBuilder>)` | Custom sources, filters, processors, samplers, or exporter pipelines |
| `ConfigureMetrics(Action<MeterProviderBuilder>)` | Custom meters, views, readers, or exporters |
| `ConfigureLogging(Action<OpenTelemetryLoggerOptions>)` | OpenTelemetry logging options and exporters |

If you configure an instrumentation manually through `ConfigureTracing`, skip the matching convenience method to avoid registering the same instrumentation twice.

## Recipes

WitnessSharp does not ship hard-coded health-check or SQL filters. Those choices depend on your app. Use the escape hatches and keep the policy in your service code.

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

This pattern is a good fit when `WithStandardInstrumentations()` is almost right, but you need a request filter.

</details>

<details>
<summary>Filter fast SQL spans with a custom processor</summary>

Duration-based SQL filtering is app-specific, so WitnessSharp leaves it to your tracing pipeline. This example keeps SQL spans that run for at least 100 ms and exports everything else as usual.

This recipe assumes you have also installed the SQL client instrumentation package from the OpenTelemetry ecosystem.

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

<details>
<summary>Add custom resource attributes</summary>

You can add shared metadata once and have it show up on logs, metrics, and traces.

```json
{
  "Witness": {
    "ServiceName": "orders-api",
    "AdditionalResourceAttributes": {
      "service.owner": "checkout",
      "cloud.region": "westeurope",
      "deployment.ring": "blue"
    }
  }
}
```

You can do the same in code if you prefer:

```csharp
builder.Services.AddWitness(options =>
{
    options.ServiceName = "orders-api";
    options.AdditionalResourceAttributes["service.owner"] = "checkout";
    options.AdditionalResourceAttributes["cloud.region"] = "westeurope";
    options.AdditionalResourceAttributes["deployment.ring"] = "blue";
});
```

</details>

## Testing

`WitnessSharp.Testing` gives you `TestWitness<T>`, an in-memory test double that records logged messages, metrics, and activities. It includes `AssertLogged(...)`, `AssertMetricRecorded(...)`, and `AssertActivityStarted(...)` assertion helpers.

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

`WitnessSharp.Analyzers` is an optional Roslyn analyzer package. Its first rule, `WS0001`, flags `witness.Logger.LogInformation(...)`, `witness.Logger.LogWarning(...)`, and `witness.Logger.Log(LogLevel, ...)` calls inside `IWitness` or `IWitness<T>` extension methods such as:

```csharp
public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId) =>
    witness.Logger.LogInformation("Order {OrderId} placed", orderId);
```

That pattern is convenient, but hot paths often benefit from the `LoggerMessage` source generator. `WS0001` nudges you toward moving the template into a dedicated generated method, and the package includes a code fix to help with the rewrite.

### Install the analyzer

```bash
dotnet add package WitnessSharp.Analyzers
```

### Configure severity in `.editorconfig`

```ini
dotnet_diagnostic.WS0001.severity = warning
```

### The `LoggerMessage` pattern it promotes

```csharp
public static partial class OrderLogs
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Order {OrderId} placed")]
    public static partial void OrderPlaced(this ILogger logger, int orderId);
}

public static class OrderServiceWitnessExtensions
{
    public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId) =>
        witness.Logger.OrderPlaced(orderId);
}
```

For background on source-generated logging, see the official [`LoggerMessage` docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator).

## AOT support

WitnessSharp is designed to stay friendly to trimming and native AOT. The core package uses standard .NET and OpenTelemetry APIs. Your final AOT story depends on the instrumentations and exporters you enable — when publishing with `PublishAot=true`, watch for warnings from upstream packages.

## Package family

| Package | Purpose |
| --- | --- |
| `WitnessSharp` | Core primitives, DI registration, `IWitness<T>`, `WitnessedAction`, options, and fluent builder extensions |
| `WitnessSharp.AzureMonitor` | Azure Monitor exporter wiring via `.WithAzureMonitor()` |
| `WitnessSharp.Analyzers` | Roslyn analyzer package with `WS0001` |
| `WitnessSharp.Testing` | `TestWitness<T>` and assertion helpers for test projects |

## Contributing

Contributions are welcome. Build with `dotnet build WitnessSharp.slnx`, test with `dotnet test WitnessSharp.slnx`, then open a pull request. If a `CONTRIBUTING.md` appears, follow that file first.

## License

MIT. See [LICENSE](LICENSE).

## Further reading

- [OpenTelemetry for .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Azure Monitor OpenTelemetry exporter](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme)
- [High-performance logging with `LoggerMessage`](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator)
