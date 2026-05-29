using System.Diagnostics;

namespace WitnessSharp;

/// <summary>Extension methods on <see cref="IWitness"/> for starting activities.</summary>
public static class WitnessExtensions
{
    /// <summary>
    /// Starts a new <see cref="WitnessedAction"/> by creating an activity on the witness's activity source.
    /// The wrapped activity may be <see langword="null"/> when no listener is sampling.
    /// </summary>
    /// <param name="witness">The witness whose activity source is used.</param>
    /// <param name="name">The activity name.</param>
    /// <returns>A disposable <see cref="WitnessedAction"/>.</returns>
    public static WitnessedAction StartAction(this IWitness witness, string name) =>
        new(witness.ActivitySource.StartActivity(name));
}
