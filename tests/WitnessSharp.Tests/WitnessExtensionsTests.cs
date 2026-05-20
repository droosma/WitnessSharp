using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;

namespace WitnessSharp.Tests;

public class WitnessExtensionsTests : IDisposable
{
    private readonly Meter _meter = new("test");
    private readonly ActivitySource _source = new("WitnessSharp.Tests");
    private readonly ActivityListener _listener;

    public WitnessExtensionsTests()
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

    private Witness<Subject> CreateWitness(ActivitySource? source = null) =>
        new(_meter, source ?? _source, NullLogger<Subject>.Instance);

    [Fact]
    public void StartAction_returns_a_WitnessedAction()
    {
        var witness = CreateWitness();

        using var action = witness.StartAction("op");

        Assert.NotNull(action);
    }

    [Fact]
    public void StartAction_creates_activity_via_witness_ActivitySource()
    {
        var witness = CreateWitness();

        using var action = witness.StartAction("op");

        Assert.NotNull(action.Activity);
        Assert.Equal("op", action.Activity!.OperationName);
    }

    [Fact]
    public void StartAction_returns_action_with_null_activity_when_source_has_no_listener()
    {
        using var orphanSource = new ActivitySource("WitnessSharp.Tests.NoListener");
        var witness = CreateWitness(orphanSource);

        using var action = witness.StartAction("op");

        Assert.Null(action.Activity);
    }
}
