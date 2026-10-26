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

### DDD and Hexagonal Architecture

Apply DDD with ports-and-adapters where warranted. Core domain: the observability primitives (`IWitness<T>`, `WitnessedAction`, `WitnessedOutcome`). Keep domain logic free of infrastructure. Ports define what core needs (e.g., `IWitnessBuilder` as configuration). Adapters wire to infrastructure (OTel SDK, Azure Monitor, DI). The monorepo naturally enforces this: `WitnessSharp` is core, `WitnessSharp.AzureMonitor` is an adapter.

## Architecture

**Package family (monorepo, single `.slnx`):**

| Package | Role |
|---------|------|
| `WitnessSharp` | Core: `IWitness<T>`, `Witness<T>`, `WitnessedAction`, options, fluent builder, DI extensions |
| `WitnessSharp.AzureMonitor` | Optional Azure Monitor exporter glue (`.WithAzureMonitor()`) |
| `WitnessSharp.Analyzers` | Roslyn analyzer (`WS0001`) nudging toward `[LoggerMessage]` |
| `WitnessSharp.Testing` | `TestWitness<T>` test doubles for assertion |

**Key types:**

- `IWitness<T>` — single injectable per call site; mirrors `ILogger<T>` (no `ForType<TNew>()`).
- `IWitnessFactory` — creates `IWitness<T>` at runtime (e.g., constructing child objects).
- `Witness<T>` — sealed singleton; exposes `Meter`, `ActivitySource`, `ILogger<T>`.
- `WitnessedAction` — disposable wrapping `Activity` with `Outcome` (Success/Failure/Cancelled).
- `WitnessedOutcome` — enum (Success/Failure/Cancelled); matched-adjective with `WitnessedAction`.
- `WitnessOptions` — config-bindable options (service name, namespace, version, etc.).
- `IWitnessBuilder` — fluent builder returned by `AddWitness()`.

Naming convention: types drop the `Sharp` suffix (following RestSharp / CefSharp / NHibernate). `Sharp` lives at the package boundary only.

## Design Principles (priority order)

1. **Open for extension, closed for modification.** Sensible defaults that users compose on top of — never replace.
2. **Don't re-abstract things .NET already does well.** `IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter` stay canonical.
3. **Lean defaults, fluent opt-in.** The package is opinionated about shape/primitives, not about what's pre-enabled.
4. **One central injectable per call site.** `IWitness<T>` is the contribution; standard primitives remain exposed.

## Key Conventions

### Deliberate design choices — do not "fix"

- `WitnessedAction.Activity` is public (promoted from field).
- `WitnessedAction.Finish()` exists alongside `Dispose()` — callers stop without disposing.
- `WitnessedAction` is a pure primitive (no lifecycle events like `OnSuccess`/`OnFailure`); extensibility deferred to post-v1.
- `IWitness<T>` has no `ForType<TNew>()`. Sub-creation via injected `IWitnessFactory` (clean SOLID).

### Setup API conventions

- `AddWitness()` is the entry point (not `UseOpenTelemetry`).
- Registration alone (no `.With*` calls) is valid — gives DI primitives + resource attributes only.
- Behavior toggles (instrumentations, exporters) live on the fluent builder, not in options.
- Config section: `Witness` in `appsettings.json`.
- `ClearProviders()` is off by default; consumers opt in via `.ClearLoggingProviders()`.

### Logging pattern & interceptor-based optimization

The package promotes extension methods on `IWitness<T>`:

```csharp
public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId) =>
    witness.Logger.LogInformation("Order {OrderId} placed", orderId);
```

On **net9.0+/net10.0**: a source-generator interceptor transparently optimizes to `[LoggerMessage]`-equivalent allocation-free code. On **net8.0**: standard behavior; use `WS0001` analyzer + code-fix for manual optimization. This interceptor is **v1 scope** — core to the package's value proposition.

### AOT

Full AOT/trimming support is a v1 commitment for **this package's code**. Annotate unavoidable reflection with `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. CI publishes the sample app with `PublishAot=true` and treats warnings from *our code* as errors. Upstream OTel warnings are documented but don't fail CI.

### Versioning

- Central package management via `Directory.Packages.props`.
- Version derived from git tags via MinVer/nbgv — no manual `<Version>` in csproj files.
- SemVer starting at `0.1.0`.

### No custom processors in v1

No custom processors (e.g., `SqlFilteringProcessor`, `HealthCheckFilteringProcessor`). Consumers use OTel's native filtering via escape hatches (`.ConfigureTracing(...)`, `.ConfigureMetrics(...)`). README recipes show common patterns; complementary packages may be added post-v1 if demand warrants.

### What was intentionally dropped from the reference implementation

From the original `Taqa.OpenTelemetry`, **not** ported: custom processors → recipes; hardcoded source filters and health-check logic; `implicit operator ResourceBuilder`; `OpenTelemetryConfiguration` record → options + builder; `ForType<TNew>()` on interface → `IWitnessFactory`; `WitnessedAction` lifecycle events → deferred to post-v1.

## Reference Implementation

The original code being ported from lives at `D:\reference\Taqa\` (read-only). Key files:
- `Taqa.OpenTelemetry\Monitor.cs` — original `Monitor<T>` interface (renamed to `Witness<T>` in this package).
- `Taqa.OpenTelemetry\MonitoredAction.cs` — original `MonitoredAction` (renamed to `WitnessedAction`).
- `Taqa.OpenTelemetry\OpenTelemetryConfiguration.cs` — old config (replaced by `WitnessOptions` + `IWitnessBuilder`).
- `Taqa.OpenTelemetry\OpenTelemetryServiceCollectionExtensions.cs` — old DI entry point (replaced by `AddWitness`).
