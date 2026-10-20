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

## Key concepts

**`IWitness<T>`** bundles `ILogger<T>`, `Meter`, and `ActivitySource` into one injectable.

**`WitnessedAction`** wraps an `Activity` created via `witness.StartAction("Name")`. Mark outcomes with `Dispose()`, `Failed(ex)`, or `Cancelled()`:

```csharp
using var action = witness.StartAction("RetrieveSummary");
try { return await _controller.RetrieveSummaryAsync(); }
catch (Exception ex) { action.Failed(ex); throw; }
```

**Logging extension methods** on `IWitness<T>` are optimized on **net9.0+** via source-generator interceptor; on **net8.0**, use the `WS0001` analyzer's code-fix:

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

## Configuration

Configure from `appsettings.json`:

```json
{
  "Witness": {
    "ServiceName": "orders-api",
    "ServiceNamespace": "Contoso.Commerce",
    "ServiceVersion": "1.3.0",
    "ServiceInstanceId": "orders-api-01",
    "DeploymentEnvironment": "Production",
    "AdditionalResourceAttributes": { "service.owner": "checkout" }
  }
}
```

Or use the fluent builder: `AddWitness(options => { ... }).WithStandardInstrumentations().WithOtlpExporter()` for direct OTel SDK access via `.ConfigureTracing(...)`, `.ConfigureMetrics(...)`, etc.

## Recipes

**Filtering traces**: Use `ConfigureTracing()` to customize. For example, exclude health checks:

```csharp
.ConfigureTracing(tracing => tracing.AddAspNetCoreInstrumentation(options =>
    options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health")))
```

**Azure Monitor**: Call `.WithAzureMonitor()` from `WitnessSharp.AzureMonitor`. The connection string is read from `APPLICATIONINSIGHTS_CONNECTION_STRING`.

## Testing

`WitnessSharp.Testing` provides `TestWitness<T>` to capture and assert on logs, metrics, and activities:

```csharp
using var witness = new TestWitness<OrderService>();
witness.Logger.LogInformation("Placed order 42");
witness.Meter.CreateCounter<int>("orders").Add(1);
witness.StartAction("PlaceOrder").Dispose();

witness.AssertLogged(LogLevel.Information, "Placed order");
witness.AssertMetricRecorded("orders");
witness.AssertActivityStarted("PlaceOrder");
```

## Analyzer (WS0001)

`WitnessSharp.Analyzers` suggests `[LoggerMessage]` for templated logging in extension methods. See [WS0001 rule](docs/rules/WS0001.md) and [LoggerMessage docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator).

## AOT support

WitnessSharp is AOT/trim-friendly. Upstream instrumentation and exporter packages may emit warnings when publishing with `PublishAot=true`.

## Contributing

Build with `dotnet build WitnessSharp.slnx`, test with `dotnet test WitnessSharp.slnx`, then open a pull request.

## License

MIT. See [LICENSE](LICENSE).

## Further reading

- [OpenTelemetry for .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Azure Monitor OpenTelemetry exporter](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme)
