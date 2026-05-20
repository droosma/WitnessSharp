# WitnessSharp — Implementation Plan

A small, opinionated .NET observability package built on OpenTelemetry. Ships:
- A central `IWitness<T>` abstraction bundling `ILogger<T>` + `Meter` + `ActivitySource`.
- A `WitnessedAction` primitive for user-defined operations with success/failure semantics.
- A lean, fluent bootstrap that registers DI primitives and lets users opt in to instrumentations, exporters, and filters.
- A Roslyn analyzer nudging users toward `[LoggerMessage]` for structured, allocation-free logging.

Design principles, in priority order:
1. **Open for extension, closed for modification.** Sensible defaults that users compose on top of — never replace.
2. **Don't re-abstract things .NET already does well.** `IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter` stay the canonical surface.
3. **Lean defaults, fluent opt-in.** The package is opinionated about *shape and primitives*, not about *what's pre-enabled*.
4. **One central injectable per call site.** `IWitness<T>` is the package's contribution; standard primitives remain exposed underneath.

---

## Package family

All targets: **`net8.0;net10.0`** (multi-target). License: **MIT**.

| Package | Purpose |
| --- | --- |
| `WitnessSharp` | Core: `IWitness`/`IWitness<T>`/`Witness<T>`, `WitnessedAction`, options + fluent builder, DI extensions. |
| `WitnessSharp.AzureMonitor` | Optional Azure Monitor exporter glue. Adds `.WithAzureMonitor(connStr)` to the builder. Keeps the heavy Azure SDK transitive deps out of the core. |
| `WitnessSharp.Analyzers` | Roslyn analyzers, distributed as a separate NuGet (opt-in install). v1 ships **one rule** with a code-fix. |
| `WitnessSharp.Testing` | Test doubles: `TestWitness<T>` capturing logs, metrics, and started activities for assertion. |

Monorepo, single `.sln`, layout:

```
WitnessSharp/
  src/
    WitnessSharp/
    WitnessSharp.AzureMonitor/
    WitnessSharp.Analyzers/
    WitnessSharp.Testing/
  tests/
    WitnessSharp.Tests/
    WitnessSharp.AzureMonitor.Tests/
    WitnessSharp.Analyzers.Tests/
    WitnessSharp.Testing.Tests/
  samples/
    SampleWebApi/        # ASP.NET Core minimal API, traces/metrics/logs → OTLP + console
  .github/workflows/
  README.md
  LICENSE
  Directory.Build.props
  Directory.Packages.props      # central package versioning
  WitnessSharp.slnx
```

Versioning: **SemVer**, start at `0.1.0` and iterate; cut `1.0` once the public API has stabilized.

---

## Core types

### `IWitness` and `IWitness<T>`

Mirrors the `ILogger` / `ILogger<T>` shape so users carry one familiar mental model.

```csharp
public interface IWitness
{
    Meter Meter { get; }                   // renamed from "Metric"
    ActivitySource ActivitySource { get; }
    ILogger Logger { get; }
}

public interface IWitness<out T> : IWitness
{
    new ILogger<T> Logger { get; }
}
```

Default implementation: **`Witness<T>`** — a regular `class` (not a record; it's a service, not a value), registered as **singleton**. A non-generic `Witness` default impl is also registered for `IWitness`.

```csharp
public sealed class Witness<T> : IWitness<T>
{
    public Meter Meter { get; }
    public ActivitySource ActivitySource { get; }
    public ILogger<T> Logger { get; }
    ILogger IWitness.Logger => Logger;

    public Witness(Meter meter, ActivitySource activitySource, ILogger<T> logger) { … }
}
```

### `IWitnessFactory`

Separate injectable for creating `IWitness<T>` instances at runtime. Replaces the former `ForType<TNew>()` on the interface (which polluted the interface with factory concerns and violated ISP).

```csharp
public interface IWitnessFactory
{
    IWitness<T> Create<T>();
}
```

Registered as singleton. Backed by `ILoggerFactory` + shared `Meter`/`ActivitySource`. Use case: a class<T> that constructs a class<B> at runtime and needs `IWitness<B>` without constructor injection of `IWitness<B>`.

Most consumers only inject `IWitness<T>` and never need the factory.

### `WitnessedAction`

A disposable primitive that manages an `Activity`'s lifecycle. Not a heavy framework — explicitly a building block. **No lifecycle events in v1** — the extensibility story is deferred until a better pattern is designed.

```csharp
public enum WitnessedOutcome { Success, Failure, Cancelled }

public sealed class WitnessedAction : IDisposable
{
    public Activity? Activity { get; }           // promoted from field → property
    public WitnessedOutcome Outcome { get; private set; } = WitnessedOutcome.Success;

    public WitnessedAction(Activity? activity) { Activity = activity; }

    // Ergonomic, null-safe pass-throughs (chainable).
    public WitnessedAction SetTag(string key, object? value) { Activity?.SetTag(key, value); return this; }
    public WitnessedAction AddEvent(string name, ActivityTagsCollection? tags = null) { … return this; }

    public void Failed(Exception? exception = null) { … Outcome = WitnessedOutcome.Failure; }
    public void Failed(string reason) { … Outcome = WitnessedOutcome.Failure; }
    public void Cancelled() { … Outcome = WitnessedOutcome.Cancelled; }
    public void Finish() => Activity?.Stop();    // kept by design

    public void Dispose()
    {
        var status = Outcome == WitnessedOutcome.Failure ? ActivityStatusCode.Error : ActivityStatusCode.Ok;
        Activity?.SetStatus(status);
        Activity?.Dispose();
    }
}
```

Canonical creator on `IWitness<T>`:

```csharp
public static class WitnessExtensions
{
    public static WitnessedAction StartAction(this IWitness witness, string name)
        => new(witness.ActivitySource.StartActivity(name));
}
```

Existing `activitySource.WitnessedAction(name)` extension stays as a low-level escape hatch.

`Activity.AddException()` is .NET 9+; on `net8.0` we polyfill via the older `Activity.RecordException()` extension under `#if NET8_0`.

### Notes on intentional non-changes

- The public `Activity` field is being **promoted to a property**, but no other "obvious cleanup" was applied. `Finish()` stays (callers may want to stop without disposing). Roslyn's `CA1051` (avoid public fields) is already moot; if any analyzer rule is noisy in this area we suppress it explicitly in the .editorconfig with a comment pointing at this plan.

---

## Setup API

### Entry point

```csharp
public static IWitnessBuilder AddWitness(this IServiceCollection services, Action<WitnessOptions> configure);
public static IWitnessBuilder AddWitness(this IServiceCollection services, IConfiguration section);
```

Returns a fluent builder so callers can chain. The non-fluent registration alone (i.e. *no* `.With*` calls) is valid — it gives you `IWitness<T>`/`Meter`/`ActivitySource` DI registration, resource attributes, and that's it.

### Options

```csharp
public sealed class WitnessOptions
{
    public string ServiceName { get; set; } = "";
    public string? ServiceNamespace { get; set; }
    public string? ServiceVersion { get; set; }
    public string? ServiceInstanceId { get; set; }      // defaults to Environment.MachineName
    public string? DeploymentEnvironment { get; set; }  // auto from DOTNET_ENVIRONMENT / ASPNETCORE_ENVIRONMENT
    public IDictionary<string, object> AdditionalResourceAttributes { get; } = new Dictionary<string, object>();
}
```

Binds from `IConfiguration` (e.g. `appsettings.json:Witness`). Behavior toggles (instrumentation, exporters, filters) live on the fluent builder, *not* in options — they're code, not config.

### Fluent builder

```csharp
public interface IWitnessBuilder
{
    IServiceCollection Services { get; }

    // Instrumentation
    IWitnessBuilder WithStandardInstrumentations();    // AspNetCore + HttpClient
    IWitnessBuilder WithAspNetCoreInstrumentation(Action<AspNetCoreTraceInstrumentationOptions>? configure = null);
    IWitnessBuilder WithHttpClientInstrumentation(Action<HttpClientTraceInstrumentationOptions>? configure = null);
    IWitnessBuilder WithSqlClientInstrumentation(Action<SqlClientTraceInstrumentationOptions>? configure = null);
    IWitnessBuilder WithEntityFrameworkCoreInstrumentation(Action<EntityFrameworkInstrumentationOptions>? configure = null);

    // Exporters
    IWitnessBuilder WithOtlpExporter(Action<OtlpExporterOptions>? configure = null);
    IWitnessBuilder WithConsoleExporter();
    // .WithAzureMonitor(...) is contributed by the WitnessSharp.AzureMonitor package.

    // Escape hatches — full access to the underlying OTel builders.
    IWitnessBuilder ConfigureTracing(Action<TracerProviderBuilder> configure);
    IWitnessBuilder ConfigureMetrics(Action<MeterProviderBuilder> configure);
    IWitnessBuilder ConfigureLogging(Action<OpenTelemetryLoggerOptions> configure);

    // Opt-in: clear existing logging providers (off by default — see below).
    IWitnessBuilder ClearLoggingProviders();
}
```

### Resource attributes (auto)

The package adds these to `ResourceBuilder` for every consumer:
- `service.name`, `service.namespace`, `service.version` — from options.
- `service.instance.id` — from options or `Environment.MachineName`.
- `telemetry.sdk.*` — added automatically by the OTel SDK.
- `deployment.environment` — from options, falling back to `DOTNET_ENVIRONMENT` / `ASPNETCORE_ENVIRONMENT`.
- Anything in `AdditionalResourceAttributes`.

### Logging providers

Lean default: **do not** call `LoggingBuilder.ClearProviders()`. The package adds the OTel logging provider alongside whatever the consumer already configured. If a consumer wants the existing behavior, they call `.ClearLoggingProviders()` explicitly.

### Dropped from the current code

- `OpenTelemetryConfiguration` record (replaced by options + builder).
- Hardcoded `"sage"` service namespace default.
- Hardcoded source filters `"Taqa.*"`, `"Azure.*"` (consumers add their own sources via `.ConfigureTracing(b => b.AddSource(...))`).
- Hardcoded health-check paths and SQL thresholds.
- `SqlFilteringProcessor`, `HealthCheckFilteringProcessor` — **no custom processors in v1**. Moved to a README "Recipes" section with copy-paste, fully parameterized examples. A complementary package may be added later.
- `implicit operator ResourceBuilder` — surprising; gone.
- `services.UseOpenTelemetry(...)` — replaced with `AddWitness(...)` to match .NET conventions.
- `IWitness<T>.ForType<TNew>()` — replaced by `IWitnessFactory` (clean SOLID separation).
- `WitnessedAction` lifecycle events (`OnSuccess`/`OnFailure`/`OnCancelled`/`OnComplete`) — deferred to post-v1.

---

## Best-practice nudges — `WitnessSharp.Analyzers`

Separate NuGet package. Users opt in.

**v1 ships two capabilities:**

### 1. Diagnostic rule: `WS0001 — Prefer [LoggerMessage] for log calls in IWitness<T> extension methods`

- Triggers when an extension method on `IWitness<T>` or `IWitness` calls `witness.Logger.LogXxx(...)` with a string template or `$"..."` interpolation.
- Severity: `Info` by default; can be promoted via .editorconfig.
- Ships with a code-fix that scaffolds a `partial` extension method on `ILogger<T>` annotated `[LoggerMessage(...)]` and rewrites the call site.
- Primary value on **net8.0** where the interceptor is not available.

### 2. Interceptor-based transparent `[LoggerMessage]` optimization (net9.0+/net10.0)

A source-generator interceptor that:
1. Detects `witness.Logger.LogXxx(...)` calls inside extension methods on `IWitness<T>`.
2. At compile time, transparently rewrites these calls to `[LoggerMessage]`-equivalent allocation-free code.
3. The consumer writes natural `ILogger` calls and gets optimized output without knowing or caring about `[LoggerMessage]`.

**Net8.0 behavior**: standard `ILogger` path (no interception). `WS0001` code-fix offers manual optimization.
**Net9.0+/net10.0 behavior**: interceptor auto-optimizes. `WS0001` is suppressed for intercepted call sites.

This interceptor is **core to the package's value proposition** — it delivers "zero ceremony, zero overhead" structured logging.

Future rules (not in v1): `WitnessedAction` must be in a `using`, `ActivitySource.StartActivity` name should be const-evaluable, avoid raw `Logger`/`ActivitySource`/`Meter` outside extension methods or test code, etc.

---

## Testing — `WitnessSharp.Testing`

```csharp
public sealed class TestWitness<T> : IWitness<T>
{
    public IReadOnlyList<LoggedMessage> LoggedMessages { get; }
    public IReadOnlyList<RecordedMetric> RecordedMetrics { get; }
    public IReadOnlyList<StartedActivity> StartedActivities { get; }

    public Meter Meter { get; }
    public ActivitySource ActivitySource { get; }
    public ILogger<T> Logger { get; }
    public IWitness<TNew> ForType<TNew>() => …;
}
```

Implementation: a `FakeLogger`-style in-memory logger (compatible with `Microsoft.Extensions.Logging.Testing`'s `FakeLogger` so users can choose), a `Meter` wired to a `MeterListener`, and an `ActivityListener` capturing started activities. Assertion-friendly snapshots; helpers like `witness.AssertLogged(LogLevel.Error, "Failed to …")`.

---

## AOT

**Full AOT support** is a v1 commitment.

- Annotate any unavoidable reflection with `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` so AOT consumers see warnings at compile time, not surprises at runtime.
- Avoid reflection in default code paths.
- CI runs the `SampleWebApi` with `dotnet publish -p:PublishAot=true` on `net10.0` and treats any AOT/trimming warnings as build failures.

---

## CI/CD

GitHub Actions. Workflows:

- **`build.yml`** — runs on PR and push to `main`:
  - Restore, build, test on a matrix of `{ net8.0, net10.0 } × { ubuntu-latest, windows-latest }`.
  - Run analyzer tests, code-fix tests.
  - Publish AOT sample build (`net10.0`, linux) and fail on AOT warnings.
  - Pack all NuGets with SourceLink + deterministic builds + `.snupkg` symbols.
  - Upload packed `.nupkg`/`.snupkg` as build artifacts (no publish).
- **`release.yml`** — triggers on git tag `v*`:
  - Re-pack (deterministic) and **publish to NuGet.org** using `NUGET_API_KEY` secret.
  - Create a GitHub Release with auto-generated notes.
  - Versions derived from tag via `MinVer` or `nbgv`; no manual `<Version>` in csproj.

Branch model: trunk-based on `main`. Tags `v0.1.0`, `v0.1.1`, … cut releases.

---

## Sample app

`samples/SampleWebApi` — minimal ASP.NET Core API:
- Calls `services.AddWitness(builder.Configuration.GetSection("Witness")).WithStandardInstrumentations().WithOtlpExporter().WithConsoleExporter();`
- One controller demonstrating: an injected `IWitness<HomeController>`, a `StartAction("Lookup")` block with tagging + `Failed(ex)`, and an extension method on `IWitness<HomeController>` using `[LoggerMessage]`.
- `appsettings.json` showing config binding.
- `README.md` with `docker compose up` for an OTLP collector + Jaeger so the sample is end-to-end runnable.

---

## Docs

- **README.md** at repo root:
  - 30-second quickstart.
  - Concepts: `IWitness<T>`, `WitnessedAction`, lean-defaults philosophy.
  - Configuration reference (options + builder).
  - Recipes section: noise-filtering health checks, fast-SQL filtering, `[LoggerMessage]` patterns, Azure Monitor wiring.
  - Testing recipes (using `WitnessSharp.Testing`).
  - AOT notes.
- **Per-package README** (shown on NuGet.org).
- No DocFX site at v1; revisit once adoption justifies the maintenance cost.

OSS housekeeping to ship at v1:
- `LICENSE` (MIT).
- `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1).
- `CONTRIBUTING.md` — how to build, test, where to file bugs.
- `.github/ISSUE_TEMPLATE/` — bug, feature.
- `.github/PULL_REQUEST_TEMPLATE.md`.

---

## Future-work register

Captured here so we don't lose context.

1. **`WitnessedAction` extensibility/notification shape.**
   - *Problem:* events (`OnSuccess`/`OnFailure`/`OnComplete`) were considered but are leak-prone (handler accumulation, swallowed exceptions) and not idiomatic for observability tooling.
   - *Decision:* ship v1 **without** lifecycle events — `WitnessedAction` is a pure primitive.
   - *Post-v1 candidates:* `IWitnessedActionObserver` registered at construction, or `Func<WitnessedAction, ValueTask>` completion callback. Design should preserve "primitive that users compose on top of" intent.

2. **Brand name.** — *Resolved 2026-05-20.* The package family is `WitnessSharp` (with `.AzureMonitor`, `.Analyzers`, `.Testing`). Internal types drop the `Sharp` suffix per the RestSharp / NHibernate convention (`IWitness<T>`, `Witness<T>`, `WitnessedAction`, `IWitnessFactory`, `WitnessOptions`, `IWitnessBuilder`, `TestWitness<T>`). Config section binds from `Witness`. Diagnostic ID prefix is `WS`.

3. **Custom processors package.**
   - *Context:* v1 ships no custom OTel processors. Consumers use OTel native filtering.
   - *Future:* if common filtering patterns emerge (health checks, fast SQL), a complementary `WitnessSharp.Processors` package can be added.

---

## Development methodology

- **TDD**: all production code is written test-first (red → green → refactor).
- **100% code coverage**: validated before moving to any next step. Covers code in this repo only.
- **Mutation testing (Stryker)**: run on core/testing/AzureMonitor packages after tests pass. Surviving mutants must be addressed. Ensures tests validate behavior, not just execute lines.
- **Analyzer package**: exempt from Stryker — the Roslyn test harness inherently validates behavior (assertions = "this code produces diagnostic X at location Y").
- **DDD**: applied as a lens when domain logic emerges. Core abstractions kept free of infrastructure concerns.
- **Hexagonal architecture**: ports (interfaces in core) and adapters (OTel/Azure wiring). The package structure enforces this naturally.
- **AOT**: CI fails on AOT/trimming warnings from our code. Upstream OTel warnings are documented but don't fail the build.

---

## v1 implementation milestones

A rough sequencing for execution, not a commitment.

1. **Scaffolding** — monorepo layout, `Directory.Build.props`/`Directory.Packages.props`, `.editorconfig`, MIT LICENSE, README skeleton, GitHub Actions `build.yml`.
2. **Core types** — `IWitness`, `IWitness<T>`, `Witness<T>`, `IWitnessFactory`, `WitnessedAction` (pure primitive with `Outcome`, no lifecycle events), `StartAction` extension. Unit tests.
3. **Setup API** — `WitnessOptions`, fluent builder, `AddWitness` overloads, resource-attribute composition. Unit + integration tests using OTel `InMemoryExporter`.
4. **`WitnessSharp.Testing`** — `TestWitness<T>` and helpers. Self-test the core package's own tests use it.
5. **`WitnessSharp.AzureMonitor`** — `.WithAzureMonitor(connStr)` extension; integration test against a fake OTLP endpoint.
6. **`WitnessSharp.Analyzers` — diagnostic** — `WS0001` rule + code-fix, analyzer test project.
7. **`WitnessSharp.Analyzers` — interceptor** — source-generator interceptor for transparent `[LoggerMessage]` optimization on net9.0+/net10.0. Tests verifying generated code output.
8. **AOT** — sample app `PublishAot=true` in CI, fix warnings in our code.
9. **Sample app** — `samples/SampleWebApi` end-to-end with docker-compose.
10. **Docs** — flesh out README (quickstart, concepts, recipes, testing, AOT, migration notes from `Taqa.OpenTelemetry`).
11. **`release.yml`** — tag-driven NuGet publish, secret wiring, dry-run with `0.1.0-preview.1`.
12. **`0.1.0` release** — tag, publish, announce.
