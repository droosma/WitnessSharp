# WitnessSharp — Implementation Plan

A small, opinionated .NET observability package built on OpenTelemetry. Provides `IWitness<T>` (bundling `ILogger<T>` + `Meter` + `ActivitySource`), `WitnessedAction` for user-defined operations, a lean fluent bootstrap, and an optional Roslyn analyzer.

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

- The public `Activity` field is being **promoted to a property**. `Finish()` stays (callers may stop without disposing). Analyzer noise in this area is suppressed explicitly in `.editorconfig` with a comment pointing at this plan.

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

    // Escape hatches — full access to the underlying OTel builders.
    IWitnessBuilder ConfigureTracing(Action<TracerProviderBuilder> configure);
    IWitnessBuilder ConfigureMetrics(Action<MeterProviderBuilder> configure);
    IWitnessBuilder ConfigureLogging(Action<OpenTelemetryLoggerOptions> configure);
}
```

Convenience methods live as **extension methods** on `IWitnessBuilder` so the interface stays minimal and sub-packages (`WitnessSharp.AzureMonitor`, future instrumentation packages) can contribute their own `WithX()` cleanly:

```csharp
// In src/WitnessSharp (WitnessBuilderExtensions)
public static IWitnessBuilder WithStandardInstrumentations(this IWitnessBuilder builder);   // AspNet + Http
public static IWitnessBuilder WithAspNetCoreInstrumentation(this IWitnessBuilder builder, Action<AspNetCoreTraceInstrumentationOptions>? configure = null);
public static IWitnessBuilder WithHttpClientInstrumentation(this IWitnessBuilder builder, Action<HttpClientTraceInstrumentationOptions>? configure = null);
public static IWitnessBuilder WithOtlpExporter(this IWitnessBuilder builder, Action<OtlpExporterOptions>? configure = null);
public static IWitnessBuilder WithConsoleExporter(this IWitnessBuilder builder);
public static IWitnessBuilder ClearLoggingProviders(this IWitnessBuilder builder);

// SqlClient + EntityFrameworkCore instrumentations live in dedicated sub-packages
// (see Future-work §4) so the core doesn't transitively depend on EFCore / SqlClient.
// .WithAzureMonitor(...) is contributed by WitnessSharp.AzureMonitor.
```

### Resource attributes (auto)

The package adds these attributes to `ResourceBuilder`:
- `service.name`, `service.namespace`, `service.version`, `service.instance.id` (or `Environment.MachineName`)
- `deployment.environment` (falls back to `DOTNET_ENVIRONMENT` / `ASPNETCORE_ENVIRONMENT`)
- `telemetry.sdk.*` (OTel SDK)
- Any keys in `AdditionalResourceAttributes`

### Logging providers

**Lean default**: do not call `LoggingBuilder.ClearProviders()`. The package adds the OTel logging provider alongside existing providers. Consumers opt in via `.ClearLoggingProviders()` if they want OTel as the only provider.

### Dropped from the current code

Intentionally excluded from the original `Taqa.OpenTelemetry`:
- `OpenTelemetryConfiguration` → options + builder
- Hardcoded filters/thresholds, `SqlFilteringProcessor`, `HealthCheckFilteringProcessor` → README recipes
- `implicit operator ResourceBuilder`, `UseOpenTelemetry()` → `AddWitness()`
- `ForType<TNew>()` → `IWitnessFactory` (cleaner SOLID separation)
- `WitnessedAction` lifecycle events → post-v1

---

## Best-practice nudges — `WitnessSharp.Analyzers`

Separate NuGet package. Users opt in.

### `WS0001`

Flags `witness.Logger.LogXxx(...)` calls with constant templates inside `IWitness<T>` extension methods. Severity: `Info` (configurable via `.editorconfig`). Includes a code-fix that scaffolds a `[LoggerMessage]` partial method.

**On `net8.0`**: Standard `ILogger` path; code-fix enables manual optimization.

**On `net9.0+/net10.0`**: A source-generator interceptor transparently rewrites calls to allocation-free `[LoggerMessage]`-equivalent code, suppressing `WS0001` for intercepted sites. Consumers write natural `ILogger` calls—no attributes required. This interceptor is **core to the package's value proposition**.

**Future rules** (post-v1): `WitnessedAction` must be in a `using`, `ActivitySource.StartActivity` name const-evaluable, avoid raw primitives outside extension methods or test code.

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

**Full AOT support** is a v1 commitment. Annotate unavoidable reflection with `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`, avoid reflection in default code paths, and treat AOT/trimming warnings from our code as CI failures (`dotnet publish -p:PublishAot=true` on `net10.0`).

---

## CI/CD

GitHub Actions. Workflows:

- **`build.yml`** — runs on PR and push to `main`: build + test matrix `{ net8.0, net10.0 } × { ubuntu-latest, windows-latest }`; AOT sample build (`net10.0`, linux) failing on AOT warnings; NuGet pack with SourceLink + `.snupkg` symbols; upload artifacts.
- **`release.yml`** — triggers on git tag `v*`: re-pack (deterministic) + publish to NuGet.org (`NUGET_API_KEY`), GitHub Release with auto-generated notes. Versions derived from tag via MinVer/nbgv — no manual `<Version>` in csproj.

Branch model: trunk-based on `main`. Tags `v0.1.0`, `v0.1.1`, … cut releases.

---

## Sample app

`samples/SampleWebApi` — minimal ASP.NET Core API demonstrating `IWitness<T>`, `StartAction`, tagging, `Failed(ex)`, and `[LoggerMessage]` extension methods with OTLP + console exporters. `docker compose up` provides Jaeger for end-to-end traces. See [`samples/SampleWebApi/README.md`](samples/SampleWebApi/README.md).

---

## Docs

- **README.md** at repo root: 30-second quickstart, concepts (`IWitness<T>`, `WitnessedAction`, lean-defaults philosophy), configuration reference, recipes (health-check / SQL filtering, Azure Monitor, `[LoggerMessage]`), testing, AOT notes.
- **Per-package README** (shown on NuGet.org). No DocFX site at v1; revisit once adoption justifies the maintenance cost.

OSS housekeeping at v1: `LICENSE` (MIT), `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1), `CONTRIBUTING.md` (build/test/filing bugs), `.github/ISSUE_TEMPLATE/` (bug + feature), `.github/PULL_REQUEST_TEMPLATE.md`.

---

## Future-work register

**Deferred to post-v1**:
- **`WitnessedAction` extensibility**: Lifecycle events deferred pending design for `IWitnessedActionObserver` or callback pattern.
- **Custom processors**: SQL filtering, health checks via OTel native filtering; dedicated package if demand warrants.
- **SqlClient / EFCore instrumentation**: Sub-packages (`WitnessSharp.Instrumentation.SqlClient`, `.EntityFrameworkCore`) avoid core transitive dependency bloat.

---

## Development methodology

Test-first (TDD) with 100% code coverage, Stryker mutation testing on core/testing/AzureMonitor packages (Analyzer uses Roslyn harness), and all tests green. DDD/hexagonal architecture where warranted; AOT warnings from our code fail CI.

---

## v1 implementation milestones

A rough sequencing for execution, not a commitment.

1. **Scaffolding** — monorepo layout, centralized package config, `.editorconfig`, MIT LICENSE, README skeleton, GitHub Actions `build.yml`
2. **Core types** — `IWitness`, `IWitness<T>`, `Witness<T>`, `IWitnessFactory`, `WitnessedAction`, `StartAction` extension, unit tests
3. **Setup API** — `WitnessOptions`, fluent builder, `AddWitness` overloads, resource-attribute composition, unit + integration tests
4. **`WitnessSharp.Testing`** — `TestWitness<T>` and assertion helpers
5. **`WitnessSharp.AzureMonitor`** — `.WithAzureMonitor(connStr)` extension, integration test with fake OTLP
6. **`WitnessSharp.Analyzers` (diagnostic)** — `WS0001` rule + code-fix, analyzer test project
7. **`WitnessSharp.Analyzers` (interceptor)** — source-generator interceptor for transparent `[LoggerMessage]` on net9.0+/net10.0, tests verifying output
8. **AOT** — sample app `PublishAot=true` in CI, fix warnings in our code
9. **Sample app** — `samples/SampleWebApi` end-to-end with docker-compose
10. **Docs** — flesh out README (quickstart, concepts, recipes, testing, AOT)
11. **`release.yml`** — tag-driven NuGet publish, dry-run with `0.1.0-preview.1`
12. **`0.1.0` release** — tag, publish, announce
