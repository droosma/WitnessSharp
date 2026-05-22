using Microsoft.Extensions.Logging;
using WitnessSharp;

namespace SampleWebApi;

public static class WeatherWitnessExtensions
{
    public static void LogForecastRequested(this IWitness<WeatherController> witness, string city)
    {
        witness.Logger.LogInformation("Weather forecast requested for {City}", city);
    }

    public static void LogForecastFailed(this IWitness<WeatherController> witness, string city, Exception ex)
    {
        witness.Logger.LogError(ex, "Failed to get forecast for {City}", city);
    }
}
