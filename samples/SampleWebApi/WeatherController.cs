using Microsoft.AspNetCore.Mvc;
using WitnessSharp;

namespace SampleWebApi;

[ApiController]
[Route("api/[controller]")]
public sealed class WeatherController(IWitness<WeatherController> witness) : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing",
        "Bracing",
        "Chilly",
        "Cool",
        "Mild",
        "Warm",
        "Balmy",
        "Hot"
    ];

    [HttpGet]
    public ActionResult<IReadOnlyList<WeatherForecast>> Get([FromQuery] string city = "Amsterdam")
    {
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
                    Summaries[Random.Shared.Next(Summaries.Length)],
                    city))
                .ToArray();

            action.SetTag("weather.forecast.count", forecast.Length);
            return Ok(forecast);
        }
        catch (Exception ex)
        {
            action.SetTag("weather.failed", true);
            action.Failed(ex);
            witness.LogForecastFailed(city, ex);

            return Problem(
                title: "Unable to get forecast",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    public sealed record WeatherForecast(DateOnly Date, int TemperatureC, string Summary, string City)
    {
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }
}
