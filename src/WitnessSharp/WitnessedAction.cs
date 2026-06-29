using System.Diagnostics;

namespace WitnessSharp;

/// <summary>
/// A disposable primitive that wraps an <see cref="System.Diagnostics.Activity"/> and tracks the
/// <see cref="WitnessedOutcome"/> of an operation. Disposing the action finalizes the activity status.
/// </summary>
public class WitnessedAction : IDisposable
{
    internal const string OutcomeTagName = "witness.outcome";

    /// <summary>
    /// The default <see cref="OnSuccess"/> behaviour: sets the activity status to
    /// <see cref="ActivityStatusCode.Ok"/> and records no outcome tag.
    /// </summary>
    private static readonly Action<WitnessedAction> _defaultOnSuccess =
        action => action.Activity?.SetStatus(ActivityStatusCode.Ok);

    /// <summary>
    /// The default <see cref="OnFailure"/> behaviour: sets the activity status to
    /// <see cref="ActivityStatusCode.Error"/> and records the <c>witness.outcome</c> tag as
    /// <c>Failure</c>.
    /// </summary>
    private static readonly Action<WitnessedAction> _defaultOnFailure = action =>
                                                                       {
                                                                           action.Activity?.SetTag(OutcomeTagName, nameof(WitnessedOutcome.Failure));
                                                                           action.Activity?.SetStatus(ActivityStatusCode.Error);
                                                                       };

    /// <summary>
    /// The default <see cref="OnCancelled"/> behaviour: sets the activity status to
    /// <see cref="ActivityStatusCode.Ok"/> and records the <c>witness.outcome</c> tag as <c>Cancelled</c>,
    /// so a cancelled operation remains distinguishable from a successful one.
    /// </summary>
    private static readonly Action<WitnessedAction> _defaultOnCancelled = action =>
    {
        action.Activity?.SetTag(OutcomeTagName, nameof(WitnessedOutcome.Cancelled));
        action.Activity?.SetStatus(ActivityStatusCode.Ok);
    };

    /// <summary>Initializes a new instance of the <see cref="WitnessedAction"/> class.</summary>
    /// <param name="activity">The activity to wrap, or <see langword="null"/> when none was created.</param>
    public WitnessedAction(Activity? activity) => Activity = activity;

    /// <summary>Gets the underlying activity, or <see langword="null"/> when no listener was sampling.</summary>
    public Activity? Activity { get; }

    /// <summary>Gets the current outcome of the operation. Defaults to <see cref="WitnessedOutcome.Success"/>.</summary>
    public WitnessedOutcome Outcome { get; private set; } = WitnessedOutcome.Success;

    /// <summary>
    /// Gets or sets the handler invoked on <see cref="Dispose"/> when the <see cref="Outcome"/> is
    /// <see cref="WitnessedOutcome.Success"/>. The wrapped <see cref="Activity"/> is available via the
    /// supplied action. Defaults to <see cref="_defaultOnSuccess"/>.
    /// </summary>
    public Action<WitnessedAction> OnSuccess { get; init; } = _defaultOnSuccess;

    /// <summary>
    /// Gets or sets the handler invoked on <see cref="Dispose"/> when the <see cref="Outcome"/> is
    /// <see cref="WitnessedOutcome.Failure"/>. The wrapped <see cref="Activity"/> is available via the
    /// supplied action. Defaults to <see cref="_defaultOnFailure"/>.
    /// </summary>
    public Action<WitnessedAction> OnFailure { get; init; } = _defaultOnFailure;

    /// <summary>
    /// Gets or sets the handler invoked on <see cref="Dispose"/> when the <see cref="Outcome"/> is
    /// <see cref="WitnessedOutcome.Cancelled"/>. The wrapped <see cref="Activity"/> is available via the
    /// supplied action. Defaults to <see cref="_defaultOnCancelled"/>.
    /// </summary>
    public Action<WitnessedAction> OnCancelled { get; init; } = _defaultOnCancelled;

    /// <summary>
    /// Invokes the handler matching the current <see cref="Outcome"/> (<see cref="OnSuccess"/>,
    /// <see cref="OnFailure"/> or <see cref="OnCancelled"/>) to reflect the outcome on the activity, then
    /// disposes the underlying activity. The activity is always disposed regardless of the handler.
    /// </summary>
    public void Dispose()
    {
        var handler = Outcome switch
        {
            WitnessedOutcome.Failure => OnFailure,
            WitnessedOutcome.Cancelled => OnCancelled,
            _ => OnSuccess,
        };
        handler(this);

        Activity?.Dispose();
        GC.SuppressFinalize(this);
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
        Activity?.AddEvent(new ActivityEvent(name, tags:tags));
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
        if(exception is not null)
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
        Activity?.AddEvent(new ActivityEvent("exception",
                                             tags:new ActivityTagsCollection
                                                  {
                                                      {"exception.message", reason},
                                                  }));
    }

    /// <summary>Marks the operation as cancelled.</summary>
    public void Cancelled() => Outcome = WitnessedOutcome.Cancelled;

    /// <summary>Stops the underlying activity without disposing it. No-op when there is no activity.</summary>
    public void Finish() => Activity?.Stop();

    private void RecordException(Exception exception)
    {
#if NET9_0_OR_GREATER
        Activity?.AddException(exception);
#else
        Activity?.AddEvent(new ActivityEvent("exception",
                                             tags:new ActivityTagsCollection
                                                  {
                                                      {"exception.type", exception.GetType().FullName},
                                                      {"exception.message", exception.Message},
                                                      {"exception.stacktrace", exception.ToString()},
                                                  }));
#endif
    }
}
