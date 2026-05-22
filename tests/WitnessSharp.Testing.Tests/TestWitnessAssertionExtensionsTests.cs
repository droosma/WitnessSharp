using Microsoft.Extensions.Logging;

namespace WitnessSharp.Testing.Tests;

public class TestWitnessAssertionExtensionsTests
{
    private sealed record Subject;

    private static void Log(TestWitness<Subject> witness, LogLevel level, string message) =>
        witness.Logger.Log(level, new EventId(), message, null, static (state, _) => state);

    [Fact]
    public void Assertion_helpers_pass_when_condition_is_met()
    {
        using var witness = new TestWitness<Subject>();
        var counter = witness.Meter.CreateCounter<int>("orders");

        Log(witness, LogLevel.Information, "processed order 42");
        counter.Add(1);
        using (witness.StartAction("process-order"))
        {
        }

        witness.AssertLogged(LogLevel.Information, "processed order");
        witness.AssertLogged(LogLevel.Information);
        witness.AssertMetricRecorded("orders");
        witness.AssertActivityStarted("process-order");
    }

    [Fact]
    public void AssertLogged_with_message_throws_descriptive_message_when_missing()
    {
        using var witness = new TestWitness<Subject>();
        Log(witness, LogLevel.Warning, "different log");

        var exception = Assert.Throws<InvalidOperationException>(() => witness.AssertLogged(LogLevel.Information, "expected"));

        Assert.Equal("Expected log at Information containing \"expected\" but none was found. Logged messages: [Warning: different log]", exception.Message);
    }

    [Fact]
    public void AssertLogged_without_message_throws_descriptive_message_when_missing()
    {
        using var witness = new TestWitness<Subject>();
        Log(witness, LogLevel.Information, "hello");

        var exception = Assert.Throws<InvalidOperationException>(() => witness.AssertLogged(LogLevel.Error));

        Assert.Equal("Expected log at Error but none was found. Logged messages: [Information: hello]", exception.Message);
    }

    [Fact]
    public void AssertActivityStarted_throws_descriptive_message_when_missing()
    {
        using var witness = new TestWitness<Subject>();
        using (witness.StartAction("present"))
        {
        }

        var exception = Assert.Throws<InvalidOperationException>(() => witness.AssertActivityStarted("missing"));

        Assert.Equal("Expected activity \"missing\" to have been started but it was not. Started activities: [present]", exception.Message);
    }

    [Fact]
    public void AssertMetricRecorded_throws_descriptive_message_when_missing()
    {
        using var witness = new TestWitness<Subject>();
        witness.Meter.CreateCounter<int>("present").Add(1);

        var exception = Assert.Throws<InvalidOperationException>(() => witness.AssertMetricRecorded("missing"));

        Assert.Equal("Expected metric \"missing\" to have been recorded but it was not. Recorded metrics: [present]", exception.Message);
    }
}
