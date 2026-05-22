# SampleWebApi

A minimal ASP.NET Core Web API showing how to wire up `WitnessSharp` in an app.

## Run locally

From the repository root:

```bash
dotnet run --project samples/SampleWebApi/SampleWebApi.csproj
```

Then call the API:

```bash
curl "http://localhost:5053/api/weather?city=Amsterdam"
curl "http://localhost:5053/api/weather?city=fail"
```

`WithConsoleExporter()` writes telemetry to the console. For OTLP export, point `OTEL_EXPORTER_OTLP_ENDPOINT` at a running collector.

## Run with Docker Compose

From `samples/SampleWebApi`:

```bash
docker compose up
```

This starts:
- the sample API on `http://localhost:8080`
- an OpenTelemetry Collector on `http://localhost:4317`
- Jaeger UI on `http://localhost:16686`

After startup, request:

```bash
curl "http://localhost:8080/api/weather?city=Amsterdam"
```

Open Jaeger at `http://localhost:16686` to inspect the `SampleWebApi` traces.
