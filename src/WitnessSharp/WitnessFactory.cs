using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WitnessSharp;

public sealed class WitnessFactory : IWitnessFactory
{
    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;
    private readonly ILoggerFactory _loggerFactory;

    public WitnessFactory(Meter meter, ActivitySource activitySource, ILoggerFactory loggerFactory)
    {
        _meter = meter;
        _activitySource = activitySource;
        _loggerFactory = loggerFactory;
    }

    public IWitness<T> Create<T>() =>
        new Witness<T>(_meter, _activitySource, _loggerFactory.CreateLogger<T>());
}
