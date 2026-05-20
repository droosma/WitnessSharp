using System.Diagnostics;

namespace WitnessSharp;

public sealed class WitnessedAction : IDisposable
{
    public Activity? Activity { get; }
    public WitnessedOutcome Outcome { get; private set; } = WitnessedOutcome.Success;

    public WitnessedAction(Activity? activity)
    {
        Activity = activity;
    }

    public WitnessedAction SetTag(string key, object? value)
    {
        Activity?.SetTag(key, value);
        return this;
    }

    public WitnessedAction AddEvent(string name, ActivityTagsCollection? tags = null)
    {
        if (Activity is null)
        {
            return this;
        }

        var activityEvent = tags is null
            ? new ActivityEvent(name)
            : new ActivityEvent(name, tags: tags);
        Activity.AddEvent(activityEvent);
        return this;
    }

    public void Failed(Exception? exception = null)
    {
        Outcome = WitnessedOutcome.Failure;
        if (exception is not null)
        {
            RecordException(exception);
        }
    }

    public void Failed(string reason)
    {
        Outcome = WitnessedOutcome.Failure;
        Activity?.AddEvent(new ActivityEvent(
            "exception",
            tags: new ActivityTagsCollection
            {
                { "exception.message", reason },
            }));
    }

    public void Cancelled() => Outcome = WitnessedOutcome.Cancelled;

    public void Finish() => Activity?.Stop();

    public void Dispose()
    {
        var status = Outcome == WitnessedOutcome.Failure
            ? ActivityStatusCode.Error
            : ActivityStatusCode.Ok;
        Activity?.SetStatus(status);
        Activity?.Dispose();
    }

    private void RecordException(Exception exception)
    {
        if (Activity is null)
        {
            return;
        }

#if NET9_0_OR_GREATER
        Activity.AddException(exception);
#else
        Activity.AddEvent(new ActivityEvent(
            "exception",
            tags: new ActivityTagsCollection
            {
                { "exception.type", exception.GetType().FullName },
                { "exception.message", exception.Message },
                { "exception.stacktrace", exception.ToString() },
            }));
#endif
    }
}
