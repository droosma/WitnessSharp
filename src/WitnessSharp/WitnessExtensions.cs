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

    /// <summary>
    /// Starts a new <see cref="WitnessedAction{T}"/> by creating an activity on the witness's activity source.
    /// The returned action is itself an <see cref="IWitness{T}"/>, so the same <c>IWitness&lt;T&gt;</c> extension
    /// methods (such as logging helpers) can be invoked directly on it. The wrapped activity may be
    /// <see langword="null"/> when no listener is sampling.
    /// </summary>
    /// <typeparam name="T">The witness category type, carried through to the returned action.</typeparam>
    /// <param name="witness">The witness whose activity source is used and whose primitives the action exposes.</param>
    /// <param name="name">The activity name.</param>
    /// <returns>A disposable <see cref="WitnessedAction{T}"/>.</returns>
    public static WitnessedAction<T> StartAction<T>(this IWitness<T> witness, string name) =>
        new(witness, witness.ActivitySource.StartActivity(name));
}
