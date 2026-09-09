# WitnessSharp — Implementation Plan

A small, opinionated .NET observability package built on OpenTelemetry. Provides `IWitness<T>` (bundling `ILogger<T>` + `Meter` + `ActivitySource`), `WitnessedAction` for user-defined operations, a lean fluent bootstrap, and an optional Roslyn analyzer.

Design principles:
1. **Open for extension, closed for modification**
2. **Don't re-abstract .NET primitives** — use `IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter` directly
3. **Lean defaults, fluent opt-in** — opinionated about shape/primitives, not what's pre-enabled
4. **One central injectable per call site** — `IWitness<T>` is the contribution; standard primitives stay exposed

---

## Package family

All targets: **`net8.0;net10.0`** (multi-target). License: **MIT**.

| Package | Purpose |
| --- | --- |
| `WitnessSharp` | Core observability: interfaces, `Witness<T>`, `WitnessedAction`, options, fluent builder, DI extensions. |
| `WitnessSharp.AzureMonitor` | Optional Azure Monitor exporter (`.WithAzureMonitor(...)`) without core transitive dependencies. |
| `WitnessSharp.Analyzers` | Opt-in Roslyn analyzer with `WS0001` rule and code-fix (one rule in v1). |
| `WitnessSharp.Testing` | Test doubles: `TestWitness<T>` capturing logs, metrics, and activities for assertions. |

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

Mirrors `ILogger`/`ILogger<T>` shape for familiar usage:

```csharp
public interface IWitness
{
    Meter Meter { get; }
    ActivitySource ActivitySource { get; }
    ILogger Logger { get; }
}

public interface IWitness<out T> : IWitness
{
    new ILogger<T> Logger { get; }
}
```

**`Witness<T>`** (sealed class, singleton) is the default implementation; a non-generic `Witness` is also registered for `IWitness`.

### `IWitnessFactory`

Separate injectable for runtime `IWitness<T>` creation (replaces `ForType<TNew>()` to avoid ISP violation):

```csharp
public interface IWitnessFactory
{
    IWitness<T> Create<T>();
}
```

Use when a class constructs instances at runtime needing typed witnesses. Most callers only inject `IWitness<T>` directly.

### `WitnessedAction`

Disposable primitive managing an `Activity`'s lifecycle (no lifecycle events in v1; extensibility deferred).

```csharp
public enum WitnessedOutcome { Success, Failure, Cancelled }

public sealed class WitnessedAction : IDisposable
{
    public Activity? Activity { get; }
    public WitnessedOutcome Outcome { get; private set; } = WitnessedOutcome.Success;

    public WitnessedAction SetTag(string key, object? value) { … return this; }
    public WitnessedAction AddEvent(string name, ActivityTagsCollection? tags = null) { … return this; }
    public void Failed(Exception? exception = null) { … }
    public void Failed(string reason) { … }
    public void Cancelled() { … }
    public void Finish() => Activity?.Stop();
    public void Dispose() { … }
}
```

Created via `witness.StartAction("Name")` extension. The `Activity` property and `Finish()` method are by design. `Activity.AddException()` uses .NET 9+ method or `net8.0` polyfill.

---

## Setup API

### Entry point

```csharp
public static IWitnessBuilder AddWitness(this IServiceCollection services, Action<WitnessOptions> configure);
public static IWitnessBuilder AddWitness(this IServiceCollection services, IConfiguration section);
```

Returns a fluent builder. Calling alone (no `.With*` methods) is valid—registers `IWitness<T>`, `Meter`, `ActivitySource`, and resource attributes.

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
    IWitnessBuilder ConfigureTracing(Action<TracerProviderBuilder> configure);
    IWitnessBuilder ConfigureMetrics(Action<MeterProviderBuilder> configure);
    IWitnessBuilder ConfigureLogging(Action<OpenTelemetryLoggerOptions> configure);
}
```

Convenience methods as **extension methods** (keeping the interface minimal and allowing sub-packages to extend):
- `WithStandardInstrumentations()`, `WithAspNetCoreInstrumentation(...)`, `WithHttpClientInstrumentation(...)`
- `WithOtlpExporter(...)`, `WithConsoleExporter()`
- `ClearLoggingProviders()`
- `WithAzureMonitor(...)` (from `WitnessSharp.AzureMonitor`)

SqlClient/EntityFrameworkCore instrumentations deferred to dedicated sub-packages to avoid transitive dependencies.

### Resource attributes

Auto-populated from `WitnessOptions`: `service.name`, `service.namespace`, `service.version`, `service.instance.id`, `deployment.environment`, `telemetry.sdk.*`, and `AdditionalResourceAttributes`.

### Logging providers

By default, OTel logging provider is added alongside existing providers. Consumers opt in via `.ClearLoggingProviders()` to use OTel only.

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

Flags templated `ILogger` calls in `IWitness<T>` extension methods with code-fix to `[LoggerMessage]`.

- **net8.0**: Manual optimization via code-fix
- **net9.0+/net10.0**: Source-generator interceptor rewrites calls transparently to allocation-free `[LoggerMessage]` equivalents (core to value proposition)

Future rules (post-v1): require `WitnessedAction` in `using` statements, constant activity names, avoid raw primitives outside extensions/tests.

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
}
```

In-memory logger (compatible with `Microsoft.Extensions.Logging.Testing`), `Meter` + `MeterListener`, and `ActivityListener` for capture. Includes assertion helpers like `witness.AssertLogged(LogLevel.Error, "...")`.


---

## AOT

**Full AOT support** is a v1 commitment. Annotate unavoidable reflection with `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]`, avoid reflection in default code paths, and treat AOT/trimming warnings from our code as CI failures (`dotnet publish -p:PublishAot=true` on `net10.0`).

---

## CI/CD

- **`build.yml`**: Build + test matrix on Ubuntu/Windows for net8.0 and net10.0. AOT sample build on linux. Deterministic pack with SourceLink + symbols.
- **`release.yml`**: Tag-driven publish to NuGet.org and GitHub Releases. Versions from tags via MinVer/nbgv.

Branch model: trunk-based on `main`, tag-driven releases.


---

## Sample app

`samples/SampleWebApi` — minimal ASP.NET Core API demonstrating `IWitness<T>`, `StartAction`, tagging, `Failed(ex)`, and `[LoggerMessage]` extension methods with OTLP + console exporters. `docker compose up` provides Jaeger for end-to-end traces. See [`samples/SampleWebApi/README.md`](samples/SampleWebApi/README.md).

---

## Docs

- **README.md**: 30-second quickstart, concepts, configuration reference, recipes, testing, AOT notes.
- **Per-package READMEs** on NuGet.org (no DocFX site in v1).
- **OSS housekeeping**: LICENSE (MIT), CODE_OF_CONDUCT.md, CONTRIBUTING.md, issue/PR templates.

---

## Future-work register

Deferred to post-v1:
- **`WitnessedAction` extensibility**: Lifecycle events pending `IWitnessedActionObserver` design.
- **Custom processors**: OTel native filtering; dedicated package if demand warrants.
- **SqlClient / EFCore instrumentation**: Sub-packages to avoid core transitive bloat.


---

## Development methodology

Test-first (TDD) with 100% code coverage. Stryker mutation testing on core/testing/AzureMonitor packages. Analyzer uses Roslyn harness. All tests green. DDD/hexagonal architecture where warranted. AOT warnings from our code fail CI.

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
