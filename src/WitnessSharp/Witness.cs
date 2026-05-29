using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WitnessSharp;

/// <summary>
/// The default <see cref="IWitness{T}"/> implementation, registered as a singleton. Bundles the shared
/// <see cref="System.Diagnostics.Metrics.Meter"/> and <see cref="System.Diagnostics.ActivitySource"/>
/// with a typed <see cref="ILogger{TCategoryName}"/>.
/// </summary>
/// <typeparam name="T">The type used as the logger category.</typeparam>
public sealed class Witness<T> : IWitness<T>
{
    /// <inheritdoc/>
    public Meter Meter { get; }

    /// <inheritdoc/>
    public ActivitySource ActivitySource { get; }

    /// <inheritdoc/>
    public ILogger<T> Logger { get; }

    ILogger IWitness.Logger => Logger;

    /// <summary>Initializes a new instance of the <see cref="Witness{T}"/> class.</summary>
    /// <param name="meter">The shared meter.</param>
    /// <param name="activitySource">The shared activity source.</param>
    /// <param name="logger">The typed logger.</param>
    public Witness(Meter meter, ActivitySource activitySource, ILogger<T> logger)
    {
        Meter = meter;
        ActivitySource = activitySource;
        Logger = logger;
    }
}
