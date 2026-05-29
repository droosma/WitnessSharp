using System.Diagnostics;

namespace WitnessSharp;

/// <summary>
/// A disposable primitive that wraps an <see cref="System.Diagnostics.Activity"/> and tracks the
/// <see cref="WitnessedOutcome"/> of an operation. Disposing the action finalizes the activity status.
/// </summary>
public sealed class WitnessedAction : IDisposable
{
    internal const string OutcomeTagName = "witness.outcome";

    /// <summary>Gets the underlying activity, or <see langword="null"/> when no listener was sampling.</summary>
    public Activity? Activity { get; }

    /// <summary>Gets the current outcome of the operation. Defaults to <see cref="WitnessedOutcome.Success"/>.</summary>
    public WitnessedOutcome Outcome { get; private set; } = WitnessedOutcome.Success;

    /// <summary>Initializes a new instance of the <see cref="WitnessedAction"/> class.</summary>
    /// <param name="activity">The activity to wrap, or <see langword="null"/> when none was created.</param>
    public WitnessedAction(Activity? activity)
    {
        Activity = activity;
    }

    /// <summary>Sets a tag on the underlying activity. No-op when there is no activity.</summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>The same instance, to allow chaining.</returns>
    public WitnessedAction SetTag(string key, object? value)
    {
        Activity?.SetTag(key, value);
        return this;
    }

    /// <summary>Adds an event to the underlying activity. No-op when there is no activity.</summary>
    /// <param name="name">The event name.</param>
    /// <param name="tags">Optional tags to attach to the event.</param>
    /// <returns>The same instance, to allow chaining.</returns>
    public WitnessedAction AddEvent(string name, ActivityTagsCollection? tags = null)
    {
        Activity?.AddEvent(new ActivityEvent(name, tags: tags));
        return this;
    }

    /// <summary>
    /// Marks the operation as failed and, when provided, records the exception on the activity as an
    /// <c>exception</c> event using the standard OpenTelemetry exception semantics.
    /// </summary>
    /// <param name="exception">The exception that caused the failure, if any.</param>
    public void Failed(Exception? exception = null)
    {
        Outcome = WitnessedOutcome.Failure;
        if (exception is not null)
        {
            RecordException(exception);
        }
    }

    /// <summary>
    /// Marks the operation as failed and records the supplied reason as an <c>exception</c> event
    /// (with only the <c>exception.message</c> tag, since no exception object is available).
    /// </summary>
    /// <param name="reason">A human-readable failure reason.</param>
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

    /// <summary>Marks the operation as cancelled.</summary>
    public void Cancelled() => Outcome = WitnessedOutcome.Cancelled;

    /// <summary>Stops the underlying activity without disposing it. No-op when there is no activity.</summary>
    public void Finish() => Activity?.Stop();

    /// <summary>
    /// Finalizes the activity status and disposes it. A <see cref="WitnessedOutcome.Failure"/> maps to
    /// <see cref="ActivityStatusCode.Error"/>; all other outcomes map to <see cref="ActivityStatusCode.Ok"/>.
    /// For non-success outcomes the outcome name is also recorded as the <c>witness.outcome</c> tag, so a
    /// cancelled operation remains distinguishable from a successful one (the activity status description is
    /// only retained for the <see cref="ActivityStatusCode.Error"/> code and would otherwise be lost).
    /// </summary>
    public void Dispose()
    {
        var status = Outcome == WitnessedOutcome.Failure
            ? ActivityStatusCode.Error
            : ActivityStatusCode.Ok;
        if (Outcome != WitnessedOutcome.Success)
        {
            Activity?.SetTag(OutcomeTagName, Outcome.ToString());
        }

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
