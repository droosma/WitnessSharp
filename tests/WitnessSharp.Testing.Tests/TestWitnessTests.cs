using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WitnessSharp.Testing.Tests;

public class TestWitnessTests
{
    private sealed record Subject;

    private static void Log(TestWitness<Subject> witness, LogLevel level, string message, Exception? exception = null) =>
        witness.Logger.Log(level, new EventId((int)level, level.ToString()), message, exception, static (state, _) => state);

    [Fact]
    public void TestWitness_captures_log_messages_at_all_levels()
    {
        using var witness = new TestWitness<Subject>();
        var exception = new InvalidOperationException("boom");

        Log(witness, LogLevel.Trace, "trace");
        Log(witness, LogLevel.Debug, "debug");
        Log(witness, LogLevel.Information, "information");
        Log(witness, LogLevel.Warning, "warning");
        Log(witness, LogLevel.Error, "error", exception);
        Log(witness, LogLevel.Critical, "critical");

        Assert.Collection(
            witness.LoggedMessages,
            message => Assert.Equal((LogLevel.Trace, 0, "trace"), (message.Level, message.EventId.Id, message.Message)),
            message => Assert.Equal((LogLevel.Debug, 1, "debug"), (message.Level, message.EventId.Id, message.Message)),
            message => Assert.Equal((LogLevel.Information, 2, "information"), (message.Level, message.EventId.Id, message.Message)),
            message => Assert.Equal((LogLevel.Warning, 3, "warning"), (message.Level, message.EventId.Id, message.Message)),
            message =>
            {
                Assert.Equal(LogLevel.Error, message.Level);
                Assert.Equal(4, message.EventId.Id);
                Assert.Equal("error", message.Message);
                Assert.Same(exception, message.Exception);
            },
            message => Assert.Equal((LogLevel.Critical, 5, "critical"), (message.Level, message.EventId.Id, message.Message)));
    }

    [Fact]
    public void TestWitness_exposes_logger_interfaces_and_logger_capabilities()
    {
        using var witness = new TestWitness<Subject>();
        IWitness nonGenericWitness = witness;

        Assert.Same(witness.Logger, nonGenericWitness.Logger);
        Assert.True(witness.Logger.IsEnabled(LogLevel.Trace));
        Assert.Null(witness.Logger.BeginScope("scope"));
    }

    [Fact]
    public void TestWitness_captures_counter_and_histogram_measurements()
    {
        using var witness = new TestWitness<Subject>();
        var counter = witness.Meter.CreateCounter<int>("requests");
        var histogram = witness.Meter.CreateHistogram<double>("duration");

        counter.Add(3, new KeyValuePair<string, object?>("route", "/orders"));
        histogram.Record(12.5, new KeyValuePair<string, object?>("unit", "ms"));

        Assert.Collection(
            witness.RecordedMetrics,
            metric =>
            {
                Assert.Equal("requests", metric.InstrumentName);
                Assert.Equal(3, Assert.IsType<int>(metric.Value));
                Assert.Collection(
                    metric.Tags,
                    tag => Assert.Equal(("route", "/orders"), (tag.Key, tag.Value)));
            },
            metric =>
            {
                Assert.Equal("duration", metric.InstrumentName);
                Assert.Equal(12.5, Assert.IsType<double>(metric.Value));
                Assert.Collection(
                    metric.Tags,
                    tag => Assert.Equal(("unit", "ms"), (tag.Key, tag.Value)));
            });
    }

    [Fact]
    public void TestWitness_captures_started_activities_directly_and_via_StartAction()
    {
        using var witness = new TestWitness<Subject>();

        using (var activity = witness.ActivitySource.StartActivity("direct-activity"))
        {
            Assert.NotNull(activity);
            activity!.SetTag("kind", "direct");
        }

        using (var action = witness.StartAction("extension-activity"))
        {
            action.SetTag("kind", "extension");
        }

        Assert.Collection(
            witness.StartedActivities,
            activity =>
            {
                Assert.Equal("direct-activity", activity.Name);
                Assert.Equal(ActivityStatusCode.Unset, activity.Status);
                Assert.Collection(
                    activity.Tags,
                    tag => Assert.Equal(("kind", "direct"), (tag.Key, tag.Value)));
            },
            activity =>
            {
                Assert.Equal("extension-activity", activity.Name);
                Assert.Equal(ActivityStatusCode.Ok, activity.Status);
                Assert.Collection(
                    activity.Tags,
                    tag => Assert.Equal(("kind", "extension"), (tag.Key, tag.Value)));
            });
    }

    [Fact]
    public void Dispose_cleans_up_resources()
    {
        var witness = new TestWitness<Subject>();
        var counter = witness.Meter.CreateCounter<int>("requests");

        counter.Add(1);
        witness.Dispose();
        counter.Add(2);

        Assert.Single(witness.RecordedMetrics);
        Assert.Null(witness.ActivitySource.StartActivity("after-dispose"));
    }
}
