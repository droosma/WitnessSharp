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

The main injectable bundling `ILogger<T>`, `Meter`, and `ActivitySource` with no new abstractions. Most classes only need `IWitness<T>`.

### `WitnessedAction`

Wraps an `Activity`. Start with `witness.StartAction("Name")`, attach tags/events, and dispose when done. Outcomes default to success; call `Failed(Exception)`, `Failed(string)`, or `Cancelled()` as needed.

```csharp
using var action = witness.StartAction("RetrieveSummary");
try
{
    var summary = await _controller.RetrieveSummaryAsync();
    return summary;
}
catch (Exception ex)
{
    action.Failed(ex);
    throw;
}
```

### Logging via extension methods

Write extension methods on `IWitness<T>` for recurring log messages. The analyzer package suggests the `[LoggerMessage]` pattern for performance:

```csharp
public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId) =>
    witness.Logger.LogInformation("Order {OrderId} placed", orderId);
```

## Installation

```bash
dotnet add package WitnessSharp
dotnet add package WitnessSharp.AzureMonitor  # optional
dotnet add package WitnessSharp.Analyzers     # optional
dotnet add package WitnessSharp.Testing       # test projects
```

## Configuration reference

Configure from `appsettings.json`:

**`appsettings.json`:**

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
      "cloud.region": "westeurope"
    }
  }
}
```

Or via C# options: `builder.Services.AddWitness(options => options.ServiceName = "orders-api");`

**Fluent builder methods** like `WithStandardInstrumentations()`, `WithOtlpExporter()`, `ClearLoggingProviders()`, and individual instrumentations (`WithAspNetCoreInstrumentation()`, etc.). Use `ConfigureTracing()`, `ConfigureMetrics()`, or `ConfigureLogging()` for direct OTel SDK customization.

⚠️ Don't mix convenience methods and escape-hatch methods for the same instrumentation.

## Recipes

### Filtering traces

Filter health-check endpoints via `ConfigureTracing()`:

```csharp
.ConfigureTracing(tracing =>
{
    tracing.AddAspNetCoreInstrumentation(options =>
    {
        options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
    });
})
```

For duration-based filtering, implement a custom `BaseProcessor<Activity>` and register via `ConfigureTracing()`.

### Azure Monitor

Use `.WithAzureMonitor()` (from `WitnessSharp.AzureMonitor` package). Connection string is read from `APPLICATIONINSIGHTS_CONNECTION_STRING`. See [Azure Monitor docs](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme).

## Testing

`WitnessSharp.Testing` provides `TestWitness<T>` with assertion helpers (`AssertLogged`, `AssertMetricRecorded`, `AssertActivityStarted`):

```csharp
using var witness = new TestWitness<OrderService>();
witness.Logger.LogInformation("Placed order 42");
witness.Meter.CreateCounter<int>("orders").Add(1);
witness.StartAction("PlaceOrder").Dispose();

witness.AssertLogged(LogLevel.Information, "Placed order");
witness.AssertMetricRecorded("orders");
witness.AssertActivityStarted("PlaceOrder");
```

## Analyzer (`WS0001`)

`WitnessSharp.Analyzers` suggests the `[LoggerMessage]` pattern for templated logging in `IWitness<T>` extension methods. Configure via `.editorconfig`: `dotnet_diagnostic.WS0001.severity = warning`. See [LoggerMessage docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator).

## AOT support

WitnessSharp is AOT/trim-friendly. Upstream instrumentation and exporter packages may emit warnings when publishing with `PublishAot=true`.

## Contributing

Contributions welcome. Build with `dotnet build WitnessSharp.slnx`, test with `dotnet test WitnessSharp.slnx`, then open a pull request. Follow `CONTRIBUTING.md` if present.

## License

MIT. See [LICENSE](LICENSE).

## Further reading

- [OpenTelemetry for .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Azure Monitor OpenTelemetry exporter](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme)
