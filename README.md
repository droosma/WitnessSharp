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

The main injectable bundling `ILogger<T>`, `Meter`, and `ActivitySource` with no new abstractions. Most classes only need `IWitness<T>`; for runtime witness creation, inject `IWitnessFactory` and call `Create<T>()`.

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

Write extension methods on `IWitness<T>` for recurring log messages:

```csharp
public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId) =>
    witness.Logger.LogInformation("Order {OrderId} placed", orderId);
```

The analyzer package suggests the `[LoggerMessage]` pattern for performance.

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
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"))
    .WithStandardInstrumentations()
    .WithOtlpExporter();

// Or via options
builder.Services.AddWitness(options => options.ServiceName = "orders-api");
```

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

**`WitnessOptions` properties:**

| Property | Description | Default |
| --- | --- | --- |
| `ServiceName` | Service identity (`service.name`). | Empty string |
| `ServiceNamespace` | Service namespace grouping. | `null` |
| `ServiceVersion` | Service version tag. | `null` |
| `ServiceInstanceId` | Instance identifier. | `Environment.MachineName` |
| `DeploymentEnvironment` | Environment tag. | `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT` |
| `AdditionalResourceAttributes` | Extra resource attributes. | Empty dictionary |

### Builder options

**Instrumentations & exporters:**
- `WithStandardInstrumentations()` — ASP.NET Core + HttpClient tracing
- `WithAspNetCoreInstrumentation(...)` / `WithHttpClientInstrumentation(...)` — individual instrumentations
- `WithOtlpExporter(...)` / `WithConsoleExporter()` — trace/metric/log exporters
- `WithAzureMonitor(...)` — Azure Monitor integration (from `WitnessSharp.AzureMonitor`)
- `ClearLoggingProviders()` — clear non-OTel logging providers

**Escape hatches** (full control):
- `ConfigureTracing(...)` — custom sources, filters, processors, or samplers
- `ConfigureMetrics(...)` — custom meters, views, or readers
- `ConfigureLogging(...)` — logging options and exporters

Don't register the same instrumentation both via convenience methods and escape hatches (traces export twice).

## Recipes

WitnessSharp ships no built-in filters; use escape hatches to add custom filtering or processing.

<details>
<summary>Filter health-check and readiness spans</summary>

```csharp
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"))
    .ConfigureTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = ctx =>
                !ctx.Request.Path.StartsWithSegments("/health") &&
                !ctx.Request.Path.StartsWithSegments("/ready");
        });
        tracing.AddHttpClientInstrumentation();
    })
    .WithOtlpExporter();
```

</details>

<details>
<summary>Filter spans by duration (custom processor example)</summary>

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

Register with duration threshold (e.g., SQL slower than 100 ms):

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

⚠️ Don't combine with `.WithOtlpExporter()` or traces export twice.

</details>

<details>
<summary>Export all three signals to Azure Monitor</summary>

```csharp
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"))
    .WithStandardInstrumentations()
    .WithAzureMonitor(options =>
    {
        options.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
    });
```

If `APPLICATIONINSIGHTS_CONNECTION_STRING` is set in the environment, use `.WithAzureMonitor()` without arguments. See [Azure Monitor OpenTelemetry exporter docs](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme).

</details>

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

`WitnessSharp.Analyzers` flags templated `ILogger` calls in `IWitness<T>` extension methods and suggests the `[LoggerMessage]` pattern. Configure severity via `.editorconfig`: `dotnet_diagnostic.WS0001.severity = warning`. See [LoggerMessage docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator).

## AOT support

WitnessSharp is AOT/trim-friendly. Upstream instrumentation and exporter packages may emit warnings when publishing with `PublishAot=true`.

## Contributing

Contributions welcome. Build with `dotnet build WitnessSharp.slnx`, test with `dotnet test WitnessSharp.slnx`, then open a pull request. Follow `CONTRIBUTING.md` if present.

## License

MIT. See [LICENSE](LICENSE).

## Further reading

- [OpenTelemetry for .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [Azure Monitor OpenTelemetry exporter](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.exporter-readme)
