using Microsoft.Extensions.Logging;

namespace WitnessSharp.Testing;

/// <summary>Assertion helpers for inspecting a <see cref="TestWitness{T}"/>.</summary>
public static class TestWitnessAssertionExtensions
{
    /// <summary>Asserts that a message was logged at the given level containing the given substring.</summary>
    /// <typeparam name="T">The witness category type.</typeparam>
    /// <param name="witness">The test witness to inspect.</param>
    /// <param name="level">The expected log level.</param>
    /// <param name="messageContains">A substring expected in the logged message.</param>
    /// <exception cref="InvalidOperationException">Thrown when no matching message was logged.</exception>
    public static void AssertLogged<T>(this TestWitness<T> witness, LogLevel level, string messageContains)
    {
        if (!witness.LoggedMessages.Any(message => message.Level == level && message.Message.Contains(messageContains, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Expected log at {level} containing \"{messageContains}\" but none was found. Logged messages: [{string.Join(", ", witness.LoggedMessages.Select(message => $"{message.Level}: {message.Message}"))}]");
        }
    }

    /// <summary>Asserts that at least one message was logged at the given level.</summary>
    /// <typeparam name="T">The witness category type.</typeparam>
    /// <param name="witness">The test witness to inspect.</param>
    /// <param name="level">The expected log level.</param>
    /// <exception cref="InvalidOperationException">Thrown when no message was logged at the level.</exception>
    public static void AssertLogged<T>(this TestWitness<T> witness, LogLevel level)
    {
        if (!witness.LoggedMessages.Any(message => message.Level == level))
        {
            throw new InvalidOperationException($"Expected log at {level} but none was found. Logged messages: [{string.Join(", ", witness.LoggedMessages.Select(message => $"{message.Level}: {message.Message}"))}]");
        }
    }

    /// <summary>Asserts that an activity with the given name was started.</summary>
    /// <typeparam name="T">The witness category type.</typeparam>
    /// <param name="witness">The test witness to inspect.</param>
    /// <param name="activityName">The expected activity name.</param>
    /// <exception cref="InvalidOperationException">Thrown when no matching activity was started.</exception>
    public static void AssertActivityStarted<T>(this TestWitness<T> witness, string activityName)
    {
        if (!witness.StartedActivities.Any(activity => activity.Name == activityName))
        {
            throw new InvalidOperationException($"Expected activity \"{activityName}\" to have been started but it was not. Started activities: [{string.Join(", ", witness.StartedActivities.Select(activity => activity.Name))}]");
        }
    }

    /// <summary>Asserts that a measurement was recorded on the instrument with the given name.</summary>
    /// <typeparam name="T">The witness category type.</typeparam>
    /// <param name="witness">The test witness to inspect.</param>
    /// <param name="instrumentName">The expected instrument name.</param>
    /// <exception cref="InvalidOperationException">Thrown when no matching metric was recorded.</exception>
    public static void AssertMetricRecorded<T>(this TestWitness<T> witness, string instrumentName)
    {
        if (!witness.RecordedMetrics.Any(metric => metric.InstrumentName == instrumentName))
        {
            throw new InvalidOperationException($"Expected metric \"{instrumentName}\" to have been recorded but it was not. Recorded metrics: [{string.Join(", ", witness.RecordedMetrics.Select(metric => metric.InstrumentName))}]");
        }
    }
}
