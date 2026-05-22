using Microsoft.Extensions.Logging;

namespace WitnessSharp.Testing;

public static class TestWitnessAssertionExtensions
{
    public static void AssertLogged<T>(this TestWitness<T> witness, LogLevel level, string messageContains)
    {
        if (!witness.LoggedMessages.Any(message => message.Level == level && message.Message.Contains(messageContains, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Expected log at {level} containing \"{messageContains}\" but none was found. Logged messages: [{string.Join(", ", witness.LoggedMessages.Select(message => $"{message.Level}: {message.Message}"))}]");
        }
    }

    public static void AssertLogged<T>(this TestWitness<T> witness, LogLevel level)
    {
        if (!witness.LoggedMessages.Any(message => message.Level == level))
        {
            throw new InvalidOperationException($"Expected log at {level} but none was found. Logged messages: [{string.Join(", ", witness.LoggedMessages.Select(message => $"{message.Level}: {message.Message}"))}]");
        }
    }

    public static void AssertActivityStarted<T>(this TestWitness<T> witness, string activityName)
    {
        if (!witness.StartedActivities.Any(activity => activity.Name == activityName))
        {
            throw new InvalidOperationException($"Expected activity \"{activityName}\" to have been started but it was not. Started activities: [{string.Join(", ", witness.StartedActivities.Select(activity => activity.Name))}]");
        }
    }

    public static void AssertMetricRecorded<T>(this TestWitness<T> witness, string instrumentName)
    {
        if (!witness.RecordedMetrics.Any(metric => metric.InstrumentName == instrumentName))
        {
            throw new InvalidOperationException($"Expected metric \"{instrumentName}\" to have been recorded but it was not. Recorded metrics: [{string.Join(", ", witness.RecordedMetrics.Select(metric => metric.InstrumentName))}]");
        }
    }
}
