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

**`WitnessedAction`** wraps an `Activity` from `witness.StartAction("Name")`. Mark outcomes with `Failed(ex)`, `Cancelled()`, or `Dispose()` (default success):

```csharp
using var action = witness.StartAction("RetrieveSummary");
try { return await _controller.RetrieveSummaryAsync(); }
catch (Exception ex) { action.Failed(ex); throw; }
```

**Logging extension methods** — write standard `ILogger` calls; **net9.0+** optimizes automatically via interceptor, **net8.0** uses the `WS0001` analyzer code-fix to eliminate allocations.

## Installation

```bash
dotnet add package WitnessSharp
dotnet add package WitnessSharp.AzureMonitor  # optional
dotnet add package WitnessSharp.Analyzers     # optional
dotnet add package WitnessSharp.Testing       # test projects
```

## Configuration

Configure via `appsettings.json` (binds to `WitnessOptions`):

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

Or use the fluent builder for direct OTel SDK access:

```csharp
AddWitness(options => { ... })
    .WithStandardInstrumentations()
    .WithOtlpExporter()
    .ConfigureTracing(...)
    .ConfigureMetrics(...)
```

## Recipes & extras

**Filter traces** via `ConfigureTracing()` — e.g., exclude health checks:

```csharp
.ConfigureTracing(tracing => tracing.AddAspNetCoreInstrumentation(options =>
    options.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health")))
```

**Azure Monitor**: `.WithAzureMonitor()` from `WitnessSharp.AzureMonitor` (reads `APPLICATIONINSIGHTS_CONNECTION_STRING`).

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

## Analyzer & AOT

`WitnessSharp.Analyzers` provides `WS0001` analyzer suggesting `[LoggerMessage]` for templated logging. See [WS0001 rule](docs/rules/WS0001.md).

WitnessSharp is AOT/trim-friendly. Upstream instrumentation and exporter packages may emit warnings with `PublishAot=true`.

## Contributing

Build with `dotnet build WitnessSharp.slnx`, test with `dotnet test WitnessSharp.slnx`, then open a pull request.

## License

MIT. See [LICENSE](LICENSE).

## Further reading

- [OpenTelemetry for .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Azure Monitor OpenTelemetry exporter](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme)
