using System.Diagnostics;

namespace WitnessSharp.Tests;

public class WitnessedActionTests : IDisposable
{
    private readonly ActivitySource _source = new("WitnessSharp.Tests");
    private readonly ActivityListener _listener;

    public WitnessedActionTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s == _source,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _source.Dispose();
    }

    private Activity StartTestActivity() =>
        _source.StartActivity("test-activity") ?? throw new InvalidOperationException("Listener did not produce an activity.");

    // ----- construction / properties -----

    [Fact]
    public void Default_outcome_is_Success()
    {
        using var action = new WitnessedAction(activity: null);

        Assert.Equal(WitnessedOutcome.Success, action.Outcome);
    }

    [Fact]
    public void Activity_property_returns_constructor_argument()
    {
        using var activity = StartTestActivity();
        using var action = new WitnessedAction(activity);

        Assert.Same(activity, action.Activity);
    }

    [Fact]
    public void Activity_property_is_null_when_constructed_with_null()
    {
        using var action = new WitnessedAction(activity: null);

        Assert.Null(action.Activity);
    }

    // ----- SetTag -----

    [Fact]
    public void SetTag_writes_tag_to_activity()
    {
        using var activity = StartTestActivity();
        using var action = new WitnessedAction(activity);

        action.SetTag("foo", "bar");

        Assert.Contains(activity.TagObjects, t => t.Key == "foo" && Equals(t.Value, "bar"));
    }

    [Fact]
    public void SetTag_returns_this_for_chaining()
    {
        using var action = new WitnessedAction(activity: null);

        var returned = action.SetTag("foo", "bar");

        Assert.Same(action, returned);
    }

    [Fact]
    public void SetTag_is_safe_when_activity_is_null()
    {
        using var action = new WitnessedAction(activity: null);

        var ex = Record.Exception(() => action.SetTag("foo", "bar"));

        Assert.Null(ex);
    }

    // ----- AddEvent -----

    [Fact]
    public void AddEvent_adds_named_event_to_activity()
    {
        using var activity = StartTestActivity();
        using var action = new WitnessedAction(activity);

        action.AddEvent("checkpoint");

        Assert.Contains(activity.Events, e => e.Name == "checkpoint");
    }

    [Fact]
    public void AddEvent_passes_tags_to_activity()
    {
        using var activity = StartTestActivity();
        using var action = new WitnessedAction(activity);
        var tags = new ActivityTagsCollection { { "k", "v" } };

        action.AddEvent("checkpoint", tags);

        var ev = Assert.Single(activity.Events, e => e.Name == "checkpoint");
        Assert.Contains(ev.Tags, t => t.Key == "k" && Equals(t.Value, "v"));
    }

    [Fact]
    public void AddEvent_returns_this_for_chaining()
    {
        using var action = new WitnessedAction(activity: null);

        var returned = action.AddEvent("checkpoint");

        Assert.Same(action, returned);
    }

    [Fact]
    public void AddEvent_is_safe_when_activity_is_null()
    {
        using var action = new WitnessedAction(activity: null);

        var ex = Record.Exception(() => action.AddEvent("checkpoint"));

        Assert.Null(ex);
    }

    // ----- Failed / Cancelled -----

    [Fact]
    public void Failed_with_no_arg_sets_outcome_to_Failure()
    {
        using var action = new WitnessedAction(activity: null);

        action.Failed();

        Assert.Equal(WitnessedOutcome.Failure, action.Outcome);
    }

    [Fact]
    public void Failed_with_exception_sets_outcome_to_Failure()
    {
        using var action = new WitnessedAction(activity: null);

        action.Failed(new InvalidOperationException("boom"));

        Assert.Equal(WitnessedOutcome.Failure, action.Outcome);
    }

    [Fact]
    public void Failed_with_exception_records_exception_event_on_activity()
    {
        using var activity = StartTestActivity();
        using var action = new WitnessedAction(activity);

        action.Failed(new InvalidOperationException("boom"));

        Assert.Contains(activity.Events, e => e.Name == "exception");
    }

    [Fact]
    public void Failed_with_null_exception_does_not_record_event()
    {
        using var activity = StartTestActivity();
        using var action = new WitnessedAction(activity);

        action.Failed(exception: null);

        Assert.DoesNotContain(activity.Events, e => e.Name == "exception");
    }

    [Fact]
    public void Failed_with_reason_sets_outcome_to_Failure()
    {
        using var action = new WitnessedAction(activity: null);

        action.Failed("something bad");

        Assert.Equal(WitnessedOutcome.Failure, action.Outcome);
    }

    [Fact]
    public void Failed_with_reason_records_event_with_message_tag()
    {
        using var activity = StartTestActivity();
        using var action = new WitnessedAction(activity);

        action.Failed("something bad");

        var ev = Assert.Single(activity.Events, e => e.Name == "exception");
        Assert.Contains(ev.Tags, t => t.Key == "exception.message" && Equals(t.Value, "something bad"));
    }

    [Fact]
    public void Cancelled_sets_outcome_to_Cancelled()
    {
        using var action = new WitnessedAction(activity: null);

        action.Cancelled();

        Assert.Equal(WitnessedOutcome.Cancelled, action.Outcome);
    }

    // ----- Finish -----

    [Fact]
    public void Finish_stops_the_activity()
    {
        var activity = StartTestActivity();
        using var action = new WitnessedAction(activity);

        action.Finish();

        Assert.True(activity.Duration > TimeSpan.Zero);
    }

    [Fact]
    public void Finish_is_safe_when_activity_is_null()
    {
        using var action = new WitnessedAction(activity: null);

        var ex = Record.Exception(() => action.Finish());

        Assert.Null(ex);
    }

    // ----- Dispose status -----

    [Fact]
    public void Dispose_sets_activity_status_to_Ok_for_Success_outcome()
    {
        var activity = StartTestActivity();
        var action = new WitnessedAction(activity);

        action.Dispose();

        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public void Dispose_sets_activity_status_to_Error_for_Failure_outcome()
    {
        var activity = StartTestActivity();
        var action = new WitnessedAction(activity);
        action.Failed();

        action.Dispose();

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public void Dispose_sets_activity_status_to_Ok_for_Cancelled_outcome()
    {
        var activity = StartTestActivity();
        var action = new WitnessedAction(activity);
        action.Cancelled();

        action.Dispose();

        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public void Dispose_is_safe_when_activity_is_null()
    {
        var action = new WitnessedAction(activity: null);

        var ex = Record.Exception(() => action.Dispose());

        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_disposes_the_activity()
    {
        var activity = StartTestActivity();
        var action = new WitnessedAction(activity);

        action.Dispose();

        // Activity.Dispose() calls Stop() internally, which sets Duration > 0.
        Assert.True(activity.Duration > TimeSpan.Zero);
    }
}
