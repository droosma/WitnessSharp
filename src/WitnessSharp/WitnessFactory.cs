using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WitnessSharp;

/// <summary>
/// The default <see cref="IWitnessFactory"/> implementation. Returns a fresh <see cref="Witness{T}"/> per
/// call, backed by the shared meter and activity source and a logger from the ambient
/// <see cref="ILoggerFactory"/>.
/// </summary>
public sealed class WitnessFactory : IWitnessFactory
{
    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Initializes a new instance of the <see cref="WitnessFactory"/> class.</summary>
    /// <param name="meter">The shared meter.</param>
    /// <param name="activitySource">The shared activity source.</param>
    /// <param name="loggerFactory">The logger factory used to create typed loggers.</param>
    public WitnessFactory(Meter meter, ActivitySource activitySource, ILoggerFactory loggerFactory)
    {
        _meter = meter;
        _activitySource = activitySource;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public IWitness<T> Create<T>() =>
        new Witness<T>(_meter, _activitySource, _loggerFactory.CreateLogger<T>());
}
