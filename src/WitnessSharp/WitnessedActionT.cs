using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WitnessSharp;

/// <summary>
/// A typed <see cref="WitnessedAction"/> that is also an <see cref="IWitness{T}"/>. It carries the
/// witness it was started from and delegates the <see cref="Meter"/>, <see cref="ActivitySource"/>
/// and <see cref="Logger"/> to it. This lets the same <c>IWitness&lt;T&gt;</c> extension methods used on
/// a witness (for example logging helpers) be invoked directly on the action, keeping call sites
/// consistent.
/// </summary>
/// <typeparam name="T">The type used as the logger category.</typeparam>
public sealed class WitnessedAction<T> : WitnessedAction, IWitness<T>
{
    private readonly IWitness<T> _witness;

    /// <summary>Initializes a new instance of the <see cref="WitnessedAction{T}"/> class.</summary>
    /// <param name="witness">The witness this action was started from; its primitives are exposed by the action.</param>
    /// <param name="activity">The activity to wrap, or <see langword="null"/> when none was created.</param>
    public WitnessedAction(IWitness<T> witness, Activity? activity) : base(activity) => _witness = witness;

    /// <inheritdoc/>
    public Meter Meter => _witness.Meter;

    /// <inheritdoc/>
    public ActivitySource ActivitySource => _witness.ActivitySource;

    /// <inheritdoc/>
    public ILogger<T> Logger => _witness.Logger;

    ILogger IWitness.Logger => _witness.Logger;

    /// <summary>
    /// Sets a tag on the underlying activity and returns this typed action so chaining keeps the
    /// <see cref="IWitness{T}"/> facet (allowing typed extension methods after the chain).
    /// </summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>The same <see cref="WitnessedAction{T}"/> instance.</returns>
    public new WitnessedAction<T> SetTag(string key, object? value)
    {
        base.SetTag(key, value);
        return this;
    }

    /// <summary>
    /// Adds an event to the underlying activity and returns this typed action so chaining keeps the
    /// <see cref="IWitness{T}"/> facet (allowing typed extension methods after the chain).
    /// </summary>
    /// <param name="name">The event name.</param>
    /// <param name="tags">Optional tags to attach to the event.</param>
    /// <returns>The same <see cref="WitnessedAction{T}"/> instance.</returns>
    public new WitnessedAction<T> AddEvent(string name, ActivityTagsCollection? tags = null)
    {
        base.AddEvent(name, tags);
        return this;
    }
}
