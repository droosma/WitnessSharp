# Copilot Instructions — WitnessSharp

## Overview

WitnessSharp is a lean .NET observability package built on OpenTelemetry. It provides `IWitness<T>` (bundling `ILogger<T>` + `Meter` + `ActivitySource`), a `WitnessedAction` primitive for operation tracking, and a fluent bootstrap API. The authoritative design spec is `PLAN.md` at the repo root.

## Build & Test

```shell
# Restore + build entire solution
dotnet build WitnessSharp.slnx

# Run all tests
dotnet test WitnessSharp.slnx

# Run a single test project
dotnet test tests/WitnessSharp.Tests/WitnessSharp.Tests.csproj

# Run a single test by name
dotnet test --filter "FullyQualifiedName~MyTestMethod"

# Run tests with code coverage (must be 100%)
dotnet test WitnessSharp.slnx --collect:"XPlat Code Coverage"

# Run mutation testing via Stryker
dotnet stryker

# Pack NuGet packages locally
dotnet pack WitnessSharp.slnx -o ./artifacts
```

Multi-targets: `net8.0` and `net10.0`. CI runs on both Ubuntu and Windows. SDK is pinned via `global.json` to .NET 10.0.x stable (latest feature band, no prereleases) because the .NET 11 preview has a `dotnet sln add` regression with multi-project `.slnx` solutions.

## Development Methodology

### TDD (Test-Driven Design)

All production code is written test-first. The workflow is:
1. Write a failing test that defines the desired behavior.
2. Write the minimal production code to make it pass.
3. Refactor while keeping tests green.

### Quality Gates

- **100% code coverage** — validated via coverage tooling before considering code complete.
- **Mutation testing (Stryker)** — run after tests pass on core/testing/AzureMonitor packages to ensure tests validate behavior, not just execute lines. Analyzer package uses Roslyn harness assertions instead.
- **All tests green** — no skipped or ignored tests.

### DDD (Domain-Driven Design)

Apply DDD where the domain warrants it. In this package the core domain is the observability primitives (`IWitness<T>`, `WitnessedAction`, `WitnessedOutcome`). Keep domain logic free of infrastructure concerns.

### Hexagonal Architecture

Use ports-and-adapters separation where called for:
- **Core/domain** — pure abstractions and logic (`IWitness<T>`, `WitnessedAction`, options).
- **Ports** — interfaces defining what the core needs (e.g., `IWitnessBuilder` as a configuration port).
- **Adapters** — implementations wiring to infrastructure (OTel SDK, Azure Monitor, DI registration).

The separate packages naturally enforce this: `WitnessSharp` is the core, `WitnessSharp.AzureMonitor` is an adapter.

## Architecture

**Package family (monorepo, single `.slnx`):**

- **`WitnessSharp`** — Core: `IWitness<T>`, `Witness<T>`, `WitnessedAction`, options, fluent builder, DI extensions.
- **`WitnessSharp.AzureMonitor`** — Optional Azure Monitor exporter glue (`.WithAzureMonitor()`).
- **`WitnessSharp.Analyzers`** — Roslyn analyzer (`WS0001`) nudging toward `[LoggerMessage]`.
- **`WitnessSharp.Testing`** — `TestWitness<T>` test doubles for assertion.

**Key types:**
- `IWitness<T>` — single injectable per call site; does NOT have `ForType<TNew>()`.
- `IWitnessFactory` — singleton injectable for runtime `IWitness<T>` creation (e.g., sub-objects).
- `Witness<T>` — sealed singleton exposing `Meter`, `ActivitySource`, `ILogger<T>`.
- `WitnessedAction` — disposable primitive wrapping `Activity` with `Outcome` (Success/Failure/Cancelled).
- `WitnessedOutcome` — enum on `WitnessedAction.Outcome`.
- `WitnessOptions` — config-bindable options (service name, namespace, version).
- `IWitnessBuilder` — fluent builder returned by `AddWitness()`.

Naming: types drop the `Sharp` suffix (following RestSharp/CefSharp/NHibernate).

## Design Principles (priority order)

1. **Open for extension, closed for modification.** Sensible defaults that users compose on top of — never replace.
2. **Don't re-abstract things .NET already does well.** `IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter` stay canonical.
3. **Lean defaults, fluent opt-in.** The package is opinionated about shape/primitives, not about what's pre-enabled.
4. **One central injectable per call site.** `IWitness<T>` is the contribution; standard primitives remain exposed.

## Key Conventions

### Deliberate design choices

- `WitnessedAction.Activity` is public by design; `Finish()` exists alongside `Dispose()` to allow stopping without disposing.
- `WitnessedAction` is a pure primitive in v1 with no lifecycle events. Extensibility deferred to post-v1.
- `IWitness<T>` omits `ForType<TNew>()` in favor of `IWitnessFactory` injection (SOLID separation).

### Setup API conventions

- `AddWitness()` is the entry point (not `UseOpenTelemetry`).
- Registration alone (no `.With*` calls) is valid — gives DI primitives + resource attributes only.
- Behavior toggles (instrumentations, exporters) live on the fluent builder, not in options.
- Config section: `Witness` in `appsettings.json`.
- `ClearProviders()` is off by default; consumers opt in via `.ClearLoggingProviders()`.

### Logging pattern & interceptor-based optimization

Write extension methods on `IWitness<T>` using natural `ILogger` calls:

```csharp
public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId)
    => witness.Logger.LogInformation("Order {OrderId} placed", orderId);
```

On **net9.0+/net10.0**: a source-generator interceptor rewrites these to `[LoggerMessage]`-equivalent allocation-free code transparently. On **net8.0**: standard `ILogger` behavior; use the `WS0001` analyzer + code-fix for manual optimization. This interceptor is core to v1 value.

### AOT

Full AOT/trimming support is a v1 commitment for **this package's code**. Annotate unavoidable reflection with `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. CI publishes the sample app with `PublishAot=true` and treats warnings from *our code* as errors. Upstream OTel warnings are documented but don't fail CI.

### Versioning

- Central package management via `Directory.Packages.props`.
- Version derived from git tags via MinVer/nbgv — no manual `<Version>` in csproj files.
- SemVer starting at `0.1.0`.

### No custom processors in v1

No `SqlFilteringProcessor`, `HealthCheckFilteringProcessor`, or any custom OTel processors. Consumers use OTel's native filtering via the escape hatches (`.ConfigureTracing(...)`, `.ConfigureMetrics(...)`). README recipes show common patterns. A complementary package may be added later if demand warrants it.

### What was intentionally dropped

Not ported from original `Taqa.OpenTelemetry`:
- Custom processors (use OTel native filtering; recipes in README).
- Hardcoded filters and thresholds.
- `implicit operator ResourceBuilder`.
- `OpenTelemetryConfiguration` (replaced by options + builder).
- `ForType<TNew>()` (replaced by `IWitnessFactory`).
- Lifecycle events (deferred post-v1).

## Reference Implementation

The original code being ported from lives at `D:\reference\Taqa\` (read-only). Key files:
- `Taqa.OpenTelemetry\Monitor.cs` — original `Monitor<T>` interface (renamed to `Witness<T>` in this package).
- `Taqa.OpenTelemetry\MonitoredAction.cs` — original `MonitoredAction` (renamed to `WitnessedAction`).
- `Taqa.OpenTelemetry\OpenTelemetryConfiguration.cs` — old config (replaced by `WitnessOptions` + `IWitnessBuilder`).
- `Taqa.OpenTelemetry\OpenTelemetryServiceCollectionExtensions.cs` — old DI entry point (replaced by `AddWitness`).
