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

### Quality Gates — enforced before moving to any next step

- **100% code coverage** — of code in this repo. Validated via coverage tooling. No new code is considered complete until coverage is verified at 100%.
- **Mutation testing (Stryker)** — run after tests pass on core/testing/AzureMonitor packages. Surviving mutants must be addressed before moving on. This ensures tests validate behavior, not just execute lines.
- **Analyzer package**: the Roslyn test harness inherently validates behavior (assertions are "this code produces diagnostic X at location Y"). Stryker is not required here — the harness already guarantees behavioral testing.
- **All tests green** — no skipped or ignored tests left behind.

### DDD & Hexagonal Architecture

Core domain: observability primitives (`IWitness<T>`, `WitnessedAction`, `WitnessedOutcome`), kept free of infrastructure. Use ports-and-adapters: core abstractions (domain), interfaces defining needs (ports), implementations wiring to infrastructure (adapters). Packages enforce this naturally: `WitnessSharp` (core), `WitnessSharp.AzureMonitor` (adapter).

## Architecture

**Package family (monorepo, single `.slnx`):**

| Package | Role |
|---------|------|
| `WitnessSharp` | Core: `IWitness<T>`, `Witness<T>`, `WitnessedAction`, options, fluent builder, DI extensions |
| `WitnessSharp.AzureMonitor` | Optional Azure Monitor exporter glue (`.WithAzureMonitor()`) |
| `WitnessSharp.Analyzers` | Roslyn analyzer (`WS0001`) nudging toward `[LoggerMessage]` |
| `WitnessSharp.Testing` | `TestWitness<T>` test doubles for assertion |

**Key types:**

- `IWitness<T>` — single injectable per call site; mirrors `ILogger<T>`. No `ForType<TNew>()`.
- `IWitnessFactory` — runtime `IWitness<T>` creation for sub-objects.
- `Witness<T>` — sealed singleton exposing `Meter`, `ActivitySource`, `ILogger<T>`.
- `WitnessedAction` — disposable wrapping `Activity` with `Outcome` (Success/Failure/Cancelled); no lifecycle events in v1.
- `WitnessedOutcome` — enum: Success, Failure, Cancelled.
- `WitnessOptions` — config-bindable (service name, namespace, version, etc.).
- `IWitnessBuilder` — fluent builder from `AddWitness()`.

Naming convention: types drop the `Sharp` suffix (following RestSharp / CefSharp / NHibernate). `Sharp` lives at the package boundary only.

## Design Principles (priority order)

1. **Open for extension, closed for modification.** Sensible defaults that users compose on top of — never replace.
2. **Don't re-abstract things .NET already does well.** `IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter` stay canonical.
3. **Lean defaults, fluent opt-in.** The package is opinionated about shape/primitives, not about what's pre-enabled.
4. **One central injectable per call site.** `IWitness<T>` is the contribution; standard primitives remain exposed.

## Key Conventions

### Deliberate design choices — do not "fix"

- `WitnessedAction.Activity` is a public property (promoted from field). Keep it public.
- `WitnessedAction.Finish()` exists alongside `Dispose()` by design — callers may stop without disposing.
- `WitnessedAction` is a **pure primitive** in v1 — no lifecycle events (`OnSuccess`/`OnFailure`/etc.). Extensibility story deferred to post-v1.
- `IWitness<T>` does NOT have `ForType<TNew>()`. Sub-creation is handled by injecting `IWitnessFactory` separately (clean SOLID separation).

### Setup API conventions

- `AddWitness()` is the entry point (not `UseOpenTelemetry`).
- Registration alone (no `.With*` calls) is valid — gives DI primitives + resource attributes only.
- Behavior toggles (instrumentations, exporters) live on the fluent builder, not in options.
- Config section: `Witness` in `appsettings.json`.
- `ClearProviders()` is off by default; consumers opt in via `.ClearLoggingProviders()`.

### Logging pattern & interceptor optimization

The package promotes extension methods on `IWitness<T>`:

```csharp
public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId) =>
    witness.Logger.LogInformation("Order {OrderId} placed", orderId);
```

On **net9.0+/net10.0**: a source-generator interceptor transparently rewrites these to allocation-free `[LoggerMessage]` code. On **net8.0**: the `WS0001` analyzer suggests manual optimization. This interceptor is core to v1 value proposition.

### AOT

Full AOT/trimming support is v1 scope. Mark unavoidable reflection with `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. CI publishes sample app with `PublishAot=true` and treats warnings from our code as errors.

### Versioning

- Central package management via `Directory.Packages.props`.
- Version derived from git tags via MinVer/nbgv — no manual `<Version>` in csproj files.
- SemVer starting at `0.1.0`.

### No custom processors in v1

Consumers use OTel's native filtering via escape hatches (`.ConfigureTracing(...)`, `.ConfigureMetrics(...)`). See "Intentionally dropped" section below and README recipes for common patterns.

### Intentionally dropped from reference implementation

Not ported from `Taqa.OpenTelemetry`:
- `SqlFilteringProcessor`, `HealthCheckFilteringProcessor` (use README recipes)
- Hardcoded filters, health-check paths, SQL thresholds
- `implicit operator ResourceBuilder`, `UseOpenTelemetry()`
- `OpenTelemetryConfiguration` (use options + builder instead)
- `ForType<TNew>()` (use `IWitnessFactory`)
- `WitnessedAction` lifecycle events (post-v1)

## Reference Implementation

Original code at `D:\reference\Taqa\` (read-only). Mapping:
- `Monitor.cs` → `Witness<T>` interface
- `MonitoredAction.cs` → `WitnessedAction`
- `OpenTelemetryConfiguration.cs` → `WitnessOptions` + `IWitnessBuilder`
- `OpenTelemetryServiceCollectionExtensions.cs` → `AddWitness()`
