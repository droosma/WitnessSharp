namespace WitnessSharp;

/// <summary>The terminal outcome of a <see cref="WitnessedAction"/>.</summary>
public enum WitnessedOutcome
{
    /// <summary>The operation completed successfully.</summary>
    Success,

    /// <summary>The operation failed.</summary>
    Failure,

    /// <summary>The operation was cancelled.</summary>
    Cancelled,
}