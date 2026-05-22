namespace SampleWebApi;

public sealed record WeatherForecast(DateOnly Date, int TemperatureC, string Summary, string City)
{
    public static readonly string[] Summaries =
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

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
