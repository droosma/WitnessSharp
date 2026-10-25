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

All production code is written test-first: write a failing test, implement minimal code to pass, then refactor while keeping tests green.

### Quality Gates

- **100% code coverage** — validated via coverage tooling before considering code complete.
- **Mutation testing (Stryker)** — run after tests pass on core/testing/AzureMonitor packages. Address surviving mutants before moving on.
- **Analyzer package**: Roslyn test harness validates behavior; Stryker not required.
- **All tests green** — no skipped or ignored tests left behind.

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

| Package | Role |
|---------|------|
| `WitnessSharp` | Core: `IWitness<T>`, `Witness<T>`, `WitnessedAction`, options, fluent builder, DI extensions |
| `WitnessSharp.AzureMonitor` | Optional Azure Monitor exporter glue (`.WithAzureMonitor()`) |
| `WitnessSharp.Analyzers` | Roslyn analyzer (`WS0001`) nudging toward `[LoggerMessage]` |
| `WitnessSharp.Testing` | `TestWitness<T>` test doubles for assertion |

**Key types:**

- `IWitness<T>` — central injectable per call site; mirrors `ILogger<T>` without `ForType<TNew>()`.
- `IWitnessFactory` — injectable for runtime `IWitness<T>` creation.
- `Witness<T>` — sealed singleton exposing `Meter`, `ActivitySource`, `ILogger<T>`.
- `WitnessedAction` — disposable primitive wrapping `Activity` with `Outcome` enum (Success/Failure/Cancelled).
- `WitnessedOutcome` — outcome enum on `WitnessedAction`.
- `WitnessOptions` — config-bindable options (service name, namespace, version).
- `IWitnessBuilder` — fluent builder from `AddWitness()`.

Naming convention: types drop the `Sharp` suffix (following RestSharp / CefSharp / NHibernate). `Sharp` lives at the package boundary only.

## Design Principles (priority order)

1. **Open for extension, closed for modification.** Sensible defaults that users compose on top of — never replace.
2. **Don't re-abstract things .NET already does well.** `IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter` stay canonical.
3. **Lean defaults, fluent opt-in.** The package is opinionated about shape/primitives, not about what's pre-enabled.
4. **One central injectable per call site.** `IWitness<T>` is the contribution; standard primitives remain exposed.

## Key Conventions

**Deliberate design choices** — do not modify:
- `WitnessedAction.Activity` is public by design.
- `WitnessedAction.Finish()` exists alongside `Dispose()` for optional early stopping without disposal.
- `WitnessedAction` is a pure primitive in v1; no lifecycle events. Extensibility deferred to post-v1.
- `IWitness<T>` intentionally lacks `ForType<TNew>()`; use `IWitnessFactory` for runtime sub-creation.

**Setup API** — conventions include:
- `AddWitness()` is the entry point (not `UseOpenTelemetry`).
- Bare registration (no `.With*` calls) is valid.
- Behavior toggles live on the fluent builder, not in options.
- Config section: `Witness` in `appsettings.json`.
- `ClearProviders()` is opt-in via `.ClearLoggingProviders()`.

**Logging & optimization**:
- Promote extension methods on `IWitness<T>` that call `ILogger` naturally.
- On net9.0+/net10.0: source-generator interceptor rewrites to `[LoggerMessage]`-equivalent code at compile time.
- On net8.0: standard `ILogger` behavior; `WS0001` analyzer offers manual optimization.
- This interceptor is core to the package's v1 value proposition.

**AOT** — Full AOT/trimming support is a v1 commitment. Annotate unavoidable reflection with `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. CI treats warnings from our code as errors (via `PublishAot=true` on the sample app).

**Versioning** — Central package management via `Directory.Packages.props`. Versions derived from git tags via MinVer/nbgv; no manual `<Version>` in csproj files. SemVer from `0.1.0`.

**No custom processors in v1** — Consumers use OTel's native filtering via escape hatches (`.ConfigureTracing(...)`, `.ConfigureMetrics(...)`). README recipes show common patterns.

**Dropped from reference implementation** — These from `Taqa.OpenTelemetry` are intentionally not ported:
- `SqlFilteringProcessor`, `HealthCheckFilteringProcessor` (use README recipes).
- Hardcoded source/health-check/SQL thresholds.
- `implicit operator ResourceBuilder`.
- `OpenTelemetryConfiguration` record (replaced by options + builder).
- `ForType<TNew>()` on interface (replaced by `IWitnessFactory`).
- `WitnessedAction` lifecycle events (deferred post-v1).
