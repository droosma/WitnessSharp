using WitnessSharp;
using SampleWebApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWitness(options =>
    {
        options.ServiceName = "SampleWebApi";
        options.ServiceNamespace = "WitnessSharp.Samples";
        options.ServiceVersion = "1.0.0";
    })
    .WithStandardInstrumentations()
    .WithOtlpExporter()
    .WithConsoleExporter();

var app = builder.Build();

app.MapGet("/api/weather", (string? city, IWitness<Program> witness) =>
{
    city ??= "Amsterdam";
    using var action = witness.StartAction("GetForecast");
    action.SetTag("weather.city", city);
    witness.LogForecastRequested(city);

    try
    {
        if (string.Equals(city, "fail", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Sample failure requested.");
        }

        var forecast = Enumerable.Range(1, 5)
            .Select(index => new WeatherForecast(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(index)),
                Random.Shared.Next(-20, 36),
                WeatherForecast.Summaries[Random.Shared.Next(WeatherForecast.Summaries.Length)],
                city))
            .ToArray();

        action.SetTag("weather.forecast.count", forecast.Length);
        return Results.Ok(forecast);
    }
    catch (Exception ex)
    {
        action.SetTag("weather.failed", true);
        action.Failed(ex);
        witness.LogForecastFailed(city, ex);
        return Results.Problem(
            title: "Unable to get forecast",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.Run();
