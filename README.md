# WitnessSharp

A lean, opinionated .NET observability package built on OpenTelemetry.

> **Status:** pre-alpha. Public API is in flux. Do not depend on this yet.

## What you get

- **`IWitness<T>`** — one central injectable per call site, bundling `ILogger<T>`, `Meter`, and `ActivitySource`.
- **`WitnessedAction`** — a disposable primitive that manages an `Activity`'s lifecycle with explicit success / failure / cancellation semantics.
- **Fluent bootstrap** — `services.AddWitness(...)` registers DI primitives and resource attributes; instrumentations, exporters, and filters are opt-in via the builder.
- **Roslyn analyzer (opt-in install)** — nudges your `IWitness<T>` extension methods toward `[LoggerMessage]` for allocation-free structured logging.

## Packages

| Package | Role |
| --- | --- |
| `WitnessSharp` | Core primitives, options, fluent builder, DI extensions. |
| `WitnessSharp.AzureMonitor` | Optional Azure Monitor exporter glue (`.WithAzureMonitor()`). |
| `WitnessSharp.Analyzers` | Roslyn analyzers + `[LoggerMessage]` interceptor. |
| `WitnessSharp.Testing` | `TestWitness<T>` and assertion helpers for unit tests. |

## Quickstart

> _Coming with the first preview release. See [`PLAN.md`](PLAN.md) for the v1 spec._

```csharp
// dotnet add package WitnessSharp

services.AddWitness(cfg => cfg.ServiceName = "my-api")
        .WithStandardInstrumentations()
        .WithOtlpExporter();

public class HomeController(IWitness<HomeController> witness) { … }
```

## Design principles

1. **Open for extension, closed for modification.** Sensible defaults that compose, never replace.
2. **Don't re-abstract things .NET already does well.** `IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter` stay canonical.
3. **Lean defaults, fluent opt-in.** Opinionated about *shape*, not about *what's pre-enabled*.
4. **One central injectable per call site.**

## License

[MIT](LICENSE).
