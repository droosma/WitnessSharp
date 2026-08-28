# SampleWebApi

A minimal ASP.NET Core Web API showing how to wire up `WitnessSharp` in an app.

## Run locally

```bash
dotnet run --project samples/SampleWebApi/SampleWebApi.csproj
curl "http://localhost:5053/api/weather?city=Amsterdam"
curl "http://localhost:5053/api/weather?city=fail"
```

`WithConsoleExporter()` writes telemetry to the console. For OTLP export, point `OTEL_EXPORTER_OTLP_ENDPOINT` at a running collector.

## Run with Docker Compose

From the `samples/SampleWebApi` directory, run `docker compose up` to start the API at `http://localhost:8080` and Jaeger at `http://localhost:16686`.

```bash
curl "http://localhost:8080/api/weather?city=Amsterdam"
```
