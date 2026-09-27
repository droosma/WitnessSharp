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

### DDD (Domain-Driven Design)

Apply DDD where the domain warrants it. In this package the core domain is the observability primitives (`IWitness<T>`, `WitnessedAction`, `WitnessedOutcome`). Keep domain logic free of infrastructure concerns.

### Hexagonal Architecture

Use ports-and-adapters separation where called for:
- **Core/domain** — pure abstractions and logic (`IWitness<T>`, `WitnessedAction`, options).
- **Ports** — interfaces defining what the core needs (e.g., `IWitnessBuilder` as a configuration port).
- **Adapters** — implementations wiring to infrastructure (OTel SDK, Azure Monitor, DI registration).

The separate packages naturally enforce this: `WitnessSharp` is the core, `WitnessSharp.AzureMonitor` is an adapter.

## Architecture

See `PLAN.md` § Package family and § Core types for comprehensive details. Quick summary:

**Packages**: `WitnessSharp` (core), `WitnessSharp.AzureMonitor` (exporter), `WitnessSharp.Analyzers` (WS0001 rule), `WitnessSharp.Testing` (test doubles).

**Core injectables**: `IWitness<T>` (central, no `ForType<TNew>()`), `IWitnessFactory` (runtime creation), `Witness<T>` (sealed singleton).

**Primitives**: `WitnessedAction` (wraps `Activity`, outcome tracking), `WitnessedOutcome` (enum: Success/Failure/Cancelled).

Naming: Types drop `Sharp` suffix (RestSharp / CefSharp convention); suffix lives at package boundary only.

## Design Principles (priority order)

1. **Open for extension, closed for modification.** Sensible defaults that users compose on top of — never replace.
2. **Don't re-abstract things .NET already does well.** `IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter` stay canonical.
3. **Lean defaults, fluent opt-in.** The package is opinionated about shape/primitives, not about what's pre-enabled.
4. **One central injectable per call site.** `IWitness<T>` is the contribution; standard primitives remain exposed.

## Key Conventions

### Deliberate design choices — do not "fix"

These are intentional patterns, not bugs: `WitnessedAction.Activity` is public (promoted from field); `Finish()` exists alongside `Dispose()` (caller's choice whether to dispose); no lifecycle events in v1 (extensibility deferred); `IWitness<T>` lacks `ForType<TNew>()` (use `IWitnessFactory` instead for clean SOLID separation).

### Setup API conventions

- `AddWitness()` is the entry point (not `UseOpenTelemetry`).
- Registration alone (no `.With*` calls) is valid — gives DI primitives + resource attributes only.
- Behavior toggles (instrumentations, exporters) live on the fluent builder, not in options.
- Config section: `Witness` in `appsettings.json`.
- `ClearProviders()` is off by default; consumers opt in via `.ClearLoggingProviders()`.

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

### AOT

Full AOT/trimming support is a v1 commitment for **this package's code**. Annotate unavoidable reflection with `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]`. CI publishes the sample app with `PublishAot=true` and treats warnings from *our code* as errors. Upstream OTel warnings are documented but don't fail CI.

### Versioning

- Central package management via `Directory.Packages.props`.
- Version derived from git tags via MinVer/nbgv — no manual `<Version>` in csproj files.
- SemVer starting at `0.1.0`.

### No custom processors in v1

No `SqlFilteringProcessor`, `HealthCheckFilteringProcessor`, or any custom OTel processors. Consumers use OTel's native filtering via the escape hatches (`.ConfigureTracing(...)`, `.ConfigureMetrics(...)`). README recipes show common patterns. A complementary package may be added later if demand warrants it.

### What was intentionally dropped

These were in the original `Taqa.OpenTelemetry` but are not ported: `SqlFilteringProcessor`, `HealthCheckFilteringProcessor` (use OTel escape hatches + README recipes instead), hardcoded source/threshold filters, `implicit operator ResourceBuilder`, `OpenTelemetryConfiguration` record (replaced by options + builder), `ForType<TNew>()` (replaced by `IWitnessFactory`), lifecycle events (deferred to post-v1).

## Reference Implementation

The original code is in `D:\reference\Taqa\` (read-only). See PLAN.md for mapping of old types to new ones.
