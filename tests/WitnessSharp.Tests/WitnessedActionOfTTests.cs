using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WitnessSharp.Tests;

public class WitnessedActionOfTTests : IDisposable
{
    private readonly Meter _meter = new("WitnessSharp.Tests.OfT");
    private readonly ActivitySource _source = new("WitnessSharp.Tests.OfT");
    private readonly ActivityListener _listener;

    public WitnessedActionOfTTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s == _source,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
        _meter.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record Subject;

    private Witness<Subject> CreateWitness(ILogger<Subject>? logger = null) =>
        new(_meter, _source, logger ?? NullLogger<Subject>.Instance);

    [Fact]
    public void StartAction_on_typed_witness_returns_generic_action()
    {
        var witness = CreateWitness();

        using var action = witness.StartAction("op");

        Assert.IsType<WitnessedAction<Subject>>(action);
    }

    [Fact]
    public void Generic_action_is_an_IWitness_of_T()
    {
        var witness = CreateWitness();

        using var action = witness.StartAction("op");

        Assert.IsAssignableFrom<IWitness<Subject>>(action);
    }

    [Fact]
    public void Generic_action_exposes_the_witness_primitives()
    {
        var witness = CreateWitness();

        using var action = witness.StartAction("op");
        IWitness<Subject> facet = action;

        Assert.Same(witness.Meter, facet.Meter);
        Assert.Same(witness.ActivitySource, facet.ActivitySource);
        Assert.Same(witness.Logger, facet.Logger);
    }

    [Fact]
    public void Generic_action_exposes_logger_via_non_generic_facet()
    {
        var witness = CreateWitness();

        using var action = witness.StartAction("op");
        IWitness facet = action;

        Assert.Same(witness.Logger, facet.Logger);
    }

    [Fact]
    public void Generic_action_still_tracks_outcome_like_base()
    {
        var witness = CreateWitness();

        using var action = witness.StartAction("op");
        action.Failed("boom");

        Assert.Equal(WitnessedOutcome.Failure, action.Outcome);
    }

    [Fact]
    public void IWitness_of_T_extension_method_can_be_invoked_on_the_action()
    {
        var logger = new CapturingLogger<Subject>();
        var witness = CreateWitness(logger);

        using var action = witness.StartAction("op");
        action.LogSubjectHandled();

        Assert.Contains("subject handled", logger.Messages);
    }

    [Fact]
    public void SetTag_returns_typed_action_and_writes_tag()
    {
        var witness = CreateWitness();

        using var action = witness.StartAction("op");
        WitnessedAction<Subject> chained = action.SetTag("foo", "bar");

        Assert.Same(action, chained);
        Assert.Contains(action.Activity!.TagObjects, t => t.Key == "foo" && Equals(t.Value, "bar"));
    }

    [Fact]
    public void AddEvent_returns_typed_action_and_adds_event()
    {
        var witness = CreateWitness();

        using var action = witness.StartAction("op");
        WitnessedAction<Subject> chained = action.AddEvent("checkpoint");

        Assert.Same(action, chained);
        Assert.Contains(action.Activity!.Events, e => e.Name == "checkpoint");
    }

    [Fact]
    public void Typed_extension_resolves_after_chaining_SetTag_and_AddEvent()
    {
        var logger = new CapturingLogger<Subject>();
        var witness = CreateWitness(logger);

        using var action = witness.StartAction("op");
        action.SetTag("foo", "bar").AddEvent("checkpoint").LogSubjectHandled();

        Assert.Contains("subject handled", logger.Messages);
    }

    [Fact]
    public void Invoking_extension_on_action_logs_identically_to_invoking_on_witness()
    {
        var logger = new CapturingLogger<Subject>();
        var witness = CreateWitness(logger);

        witness.LogSubjectHandled();
        using var action = witness.StartAction("op");
        action.LogSubjectHandled();

        Assert.Equal(2, logger.Messages.Count);
        Assert.All(logger.Messages, m => Assert.Equal("subject handled", m));
    }

    private sealed class CapturingLogger<TCategory> : ILogger<TCategory>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}

internal static class WitnessedActionOfTTestExtensions
{
#pragma warning disable CA1848 // Intentionally the natural consumer pattern under test.
    public static void LogSubjectHandled<T>(this IWitness<T> witness) =>
        witness.Logger.LogInformation("subject handled");
#pragma warning restore CA1848
}
