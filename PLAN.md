# WitnessSharp — Implementation Plan

A small, opinionated .NET observability package built on OpenTelemetry. Provides `IWitness<T>` (bundling `ILogger<T>` + `Meter` + `ActivitySource`), `WitnessedAction` for user-defined operations, a lean fluent bootstrap, and an optional Roslyn analyzer.

Design principles: open for extension; don't re-abstract .NET primitives (`IConfiguration`, `IOptions`, `ILoggerFactory`, `Activity`, `Meter`); lean defaults with fluent opt-in; one central injectable (`IWitness<T>`) per call site.

---

## Package family

All targets: **`net8.0;net10.0`** (multi-target). License: **MIT**.

Monorepo layout:

| Package | Purpose |
|---------|---------|
| `WitnessSharp` | Core: `Witness<T>`, `WitnessedAction`, options, fluent builder, DI extensions. |
| `WitnessSharp.AzureMonitor` | Optional Azure Monitor exporter without core transitive dependencies. |
| `WitnessSharp.Analyzers` | Roslyn analyzer: `WS0001` rule with code-fix. |
| `WitnessSharp.Testing` | Test doubles: `TestWitness<T>` capturing logs, metrics, and activities. |

Tests mirror `src`; sample app at `samples/SampleWebApi`.

Versioning: **SemVer**, start at `0.1.0` and iterate; cut `1.0` once the public API has stabilized.

---

## Core types

### `IWitness<T>` and `IWitnessFactory`

Mirrors `ILogger`/`ILogger<T>` for familiar usage. Default: `Witness<T>` (sealed, singleton). Most call sites inject `IWitness<T>` directly; use `IWitnessFactory` for runtime instance creation.

```csharp
public interface IWitness<out T> : IWitness { new ILogger<T> Logger { get; } }
public interface IWitnessFactory { IWitness<T> Create<T>(); }
```

### `WitnessedAction`

Disposable primitive wrapping an `Activity`, created via `witness.StartAction("Name")`. Track outcomes with `Dispose()`, `Failed(ex)`, or `Cancelled()`. `Activity` property and `Finish()` method are by design. No lifecycle events in v1.

```csharp
public enum WitnessedOutcome { Success, Failure, Cancelled }

public sealed class WitnessedAction : IDisposable
{
    public Activity? Activity { get; }
    public WitnessedOutcome Outcome { get; private set; } = WitnessedOutcome.Success;
    public void Failed(Exception? exception = null) { … }
    public void Cancelled() { … }
    public void Finish() => Activity?.Stop();
}
```

---

## Setup API

**Entry point** (registers `IWitness<T>`, `Meter`, `ActivitySource`, resource attributes):

```csharp
builder.Services.AddWitness(builder.Configuration.GetSection("Witness"));
```

**Options** (binds from `appsettings.json:Witness`):

```csharp
public sealed class WitnessOptions
{
    public string ServiceName { get; set; } = "";
    public string? ServiceNamespace { get; set; }
    public string? ServiceVersion { get; set; }
    public string? ServiceInstanceId { get; set; }
    public string? DeploymentEnvironment { get; set; }
    public IDictionary<string, object> AdditionalResourceAttributes { get; } = new Dictionary<string, object>();
}
```

**Fluent builder** (optional; `AddWitness()` alone is valid):

```csharp
public interface IWitnessBuilder
{
    IWitnessBuilder ConfigureTracing(Action<TracerProviderBuilder> configure);
    IWitnessBuilder ConfigureMetrics(Action<MeterProviderBuilder> configure);
    IWitnessBuilder ConfigureLogging(Action<OpenTelemetryLoggerOptions> configure);
    // Convenience: WithStandardInstrumentations(), WithOtlpExporter(), WithConsoleExporter(), etc.
}
```

Escape hatches provide direct OTel SDK access—do not mix convenience and escape-hatch methods for the same instrumentation.

---

## Analyzer — `WitnessSharp.Analyzers` (opt-in)

**`WS0001`**: Flags templated `ILogger` calls in `IWitness<T>` extension methods. On **net8.0**, code-fix suggests `[LoggerMessage]`. On **net9.0+**, source-generator interceptor optimizes automatically.

---

## Testing — `WitnessSharp.Testing`

In-memory capture via `MeterListener`, `ActivityListener`, and logging backend. Includes assertion helpers: `AssertLogged()`, `AssertMetricRecorded()`, `AssertActivityStarted()`.

```csharp
public sealed class TestWitness<T> : IWitness<T>
{
    public IReadOnlyList<LoggedMessage> LoggedMessages { get; }
    public IReadOnlyList<RecordedMetric> RecordedMetrics { get; }
    public IReadOnlyList<StartedActivity> StartedActivities { get; }
}
```


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

- **README.md**: quickstart, concepts, configuration, recipes, testing, AOT notes
- **Per-package READMEs** on NuGet.org
- **OSS housekeeping**: LICENSE, CODE_OF_CONDUCT.md, CONTRIBUTING.md, templates

---

---

## Development methodology

Test-first (TDD): 100% code coverage, Stryker mutation testing (core/testing/AzureMonitor), all tests green. DDD/hexagonal applied where warranted. AOT warnings from our code fail CI.

---

## Post-v1 roadmap

- **`WitnessedAction` extensibility**: Lifecycle events / observers
- **Custom processors**: OTel filtering recipes or dedicated package
- **SqlClient / EFCore instrumentation**: Sub-packages to avoid bloat
