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

All production code is written test-first: write a failing test, implement minimal code to pass it, then refactor while keeping tests green.

### Quality Gates — enforced before proceeding

- **100% code coverage** — validated via tooling before any code is considered complete.
- **Mutation testing (Stryker)** — run after tests pass to ensure tests validate behavior, not just execute lines.
- **Analyzer package**: Roslyn test harness inherently validates behavior.
- **All tests green** — no skipped or ignored tests.

### DDD & Hexagonal Architecture

The core domain comprises observability primitives (`IWitness<T>`, `WitnessedAction`, `WitnessedOutcome`). Keep domain logic free of infrastructure. Use ports-and-adapters: `WitnessSharp` is the core (pure abstractions), separate packages are adapters (OTel SDK, Azure Monitor, DI).

## Architecture

**Package family (monorepo, single `.slnx`):**

| Package | Role |
|---------|------|
| `WitnessSharp` | Core: `IWitness<T>`, `Witness<T>`, `WitnessedAction`, options, fluent builder, DI extensions |
| `WitnessSharp.AzureMonitor` | Optional Azure Monitor exporter glue (`.WithAzureMonitor()`) |
| `WitnessSharp.Analyzers` | Roslyn analyzer (`WS0001`) nudging toward `[LoggerMessage]` |
| `WitnessSharp.Testing` | `TestWitness<T>` test doubles for assertion |

**Key types:**

- `IWitness<T>` — single injectable per call site; mirrors `ILogger<T>`. Does NOT have `ForType<TNew>()`.
- `IWitnessFactory` — creates `IWitness<T>` instances at runtime (replaces `ForType<TNew>()`).
- `Witness<T>` — sealed singleton implementation exposing `Meter`, `ActivitySource`, `ILogger<T>`.
- `WitnessedAction` — disposable wrapper around `Activity` with `Outcome` (Success/Failure/Cancelled).
- `WitnessedOutcome` — enum on `WitnessedAction.Outcome`.
- `WitnessOptions` — config-bindable options (service name, namespace, version, etc.).
- `IWitnessBuilder` — fluent builder returned by `AddWitness()`.

Naming convention: types drop the `Sharp` suffix. `Sharp` lives at package boundary only.

## Design Principles (priority order)

1. **Open for extension, closed for modification.** Sensible defaults that users compose on top of — never replace.
2. **Don't re-abstract things .NET already does well.** `IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter` stay canonical.
3. **Lean defaults, fluent opt-in.** The package is opinionated about shape/primitives, not about what's pre-enabled.
4. **One central injectable per call site.** `IWitness<T>` is the contribution; standard primitives remain exposed.

## Key Conventions

### Deliberate design choices — do not "fix"

- `WitnessedAction.Activity` is public by design.
- `WitnessedAction.Finish()` exists alongside `Dispose()` — callers may stop without disposing.
- `IWitness<T>` has no `ForType<TNew>()` — use `IWitnessFactory` instead (cleaner SOLID separation).
- No lifecycle events in v1; extensibility deferred.

### Setup API conventions

- `AddWitness()` is the entry point (not `UseOpenTelemetry`).
- Registration alone (no `.With*` calls) is valid — registers DI primitives + resource attributes.
- Behavior toggles live on the fluent builder, not in options.
- Config section: `Witness` in `appsettings.json`.
- `ClearProviders()` off by default; opt in via `.ClearLoggingProviders()`.

### Logging pattern & interceptor-based optimization

The package promotes extension methods on `IWitness<T>` where consumers write natural `ILogger` calls:

```csharp
public static void LogOrderPlaced(this IWitness<OrderService> witness, int orderId)
{
    witness.Logger.LogInformation("Order {OrderId} placed", orderId);
}
```

On **net9.0+/net10.0**: a source-generator interceptor transparently rewrites these calls to `[LoggerMessage]`-equivalent allocation-free code at compile time. The consumer never writes or sees `[LoggerMessage]` attributes.

On **net8.0**: standard `ILogger` behavior (no interception). The `WS0001` analyzer + code-fix offers manual optimization.

This interceptor is **v1 scope** — it's core to the package's value proposition.

### AOT Support

Full AOT/trimming support is a v1 commitment. Annotate unavoidable reflection with `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. CI publishes the sample app with `PublishAot=true` and treats warnings from our code as errors. Upstream OTel warnings are documented but don't fail CI.

### Versioning

- Central package management via `Directory.Packages.props`.
- Version derived from git tags via MinVer/nbgv — no manual `<Version>` in csproj files.
- SemVer starting at `0.1.0`.

### No custom processors in v1

No `SqlFilteringProcessor`, `HealthCheckFilteringProcessor`, or any custom OTel processors. Consumers use OTel's native filtering via the escape hatches (`.ConfigureTracing(...)`, `.ConfigureMetrics(...)`). README recipes show common patterns. A complementary package may be added later if demand warrants it.

### What was intentionally dropped from reference implementation

These lived in the original `Taqa.OpenTelemetry` but are **not** ported:
- Custom processors (`SqlFilteringProcessor`, `HealthCheckFilteringProcessor`) — use OTel's native filtering instead.
- Hardcoded source filters and health-check paths — use `.ConfigureTracing()` escape hatches.
- `implicit operator ResourceBuilder` and `OpenTelemetryConfiguration` record — replaced by options + builder.
- `ForType<TNew>()` on interface and lifecycle events — deferred to post-v1.

## Reference Implementation

Original code at `D:\reference\Taqa\` (read-only):
- `Taqa.OpenTelemetry\Monitor.cs` — renamed to `Witness<T>`.
- `Taqa.OpenTelemetry\MonitoredAction.cs` — renamed to `WitnessedAction`.
- `Taqa.OpenTelemetry\OpenTelemetryConfiguration.cs` — replaced by `WitnessOptions` + `IWitnessBuilder`.
- `Taqa.OpenTelemetry\OpenTelemetryServiceCollectionExtensions.cs` — replaced by `AddWitness`.
