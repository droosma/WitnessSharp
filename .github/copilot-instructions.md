# Copilot Instructions — WitnessSharp

**Note**: For full design rationale, see `PLAN.md` at the repo root.

## Quick Reference

WitnessSharp is a lean .NET observability package on OpenTelemetry: `IWitness<T>` bundles `ILogger<T>` + `Meter` + `ActivitySource`, with `WitnessedAction` for operation tracking. Targets `net8.0` and `net10.0`.

## Build & Test

```shell
dotnet build WitnessSharp.slnx
dotnet test WitnessSharp.slnx
dotnet test WitnessSharp.slnx --collect:"XPlat Code Coverage"  # Must be 100%
dotnet stryker  # Mutation testing on core/testing/AzureMonitor packages
```

## Development Workflow

**TDD**: Write failing test → minimal production code → refactor while green.
**Quality gates**: 100% code coverage, mutation testing (Stryker), all tests green.
**Architecture**: DDD/hexagonal patterns where warranted; core domain is observability primitives.

## Core Types

**`IWitness<T>` / `Witness<T>`**: Mirrors `ILogger<T>` shape; singleton implementation exposes `Meter`, `ActivitySource`, `ILogger<T>`.

**`IWitnessFactory`**: Runtime creation of `IWitness<T>` instances (replaces `ForType<TNew>()` for SOLID separation).

**`WitnessedAction`**: Disposable wrapping `Activity`. Created via `witness.StartAction("Name")`. Outcomes: `Success`, `Failure`, `Cancelled`. Has both `Finish()` and `Dispose()` by design.

**`WitnessOptions`**: Bindable config (service name, namespace, version, instance ID, environment, resource attributes).

**`IWitnessBuilder`**: Fluent builder returned by `AddWitness()`. Escape hatches: `ConfigureTracing()`, `ConfigureMetrics()`, `ConfigureLogging()` for direct OTel SDK access.

## Design Principles & Conventions

1. **Open for extension, closed for modification**. Sensible defaults; users compose on top.
2. **Don't re-abstract .NET primitives**. `IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter` stay canonical.
3. **Lean defaults, fluent opt-in**. Opinionated about shape/primitives, not about what's pre-enabled.
4. **One injectable per call site**: `IWitness<T>` bundles primitives; standard exports remain exposed.

**Setup API**: `AddWitness()` is the entry point. Registration alone (no `.With*` calls) is valid. Behavior toggles live on the builder, not in options. Config section: `Witness` in `appsettings.json`.

**Naming**: Types drop the `Sharp` suffix (following RestSharp / CefSharp / NHibernate). `Sharp` lives at package boundary only.

**Deliberate design choices** (do not "fix"):
- `WitnessedAction.Activity` is public by design.
- `WitnessedAction.Finish()` exists alongside `Dispose()` — callers may stop without disposing.
- No lifecycle events in v1 (extensibility deferred to post-v1).
- No `ForType<TNew>()` on interface (use `IWitnessFactory` instead).

**Logging**: Promote extension methods on `IWitness<T>` for natural logging. On **net9.0+/net10.0**: source-generator interceptor transparently rewrites calls to `[LoggerMessage]` equivalents. On **net8.0**: standard `ILogger` behavior; `WS0001` analyzer offers manual optimization.

**AOT**: Full AOT/trimming support is v1 commitment. Annotate unavoidable reflection with `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`. CI publishes sample with `PublishAot=true` and treats our warnings as errors.

**No custom processors in v1**: Consumers use OTel's native filtering via escape hatches (`.ConfigureTracing(...)`, `.ConfigureMetrics(...)`). README shows common patterns.
