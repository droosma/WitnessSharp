using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WitnessSharp;

public sealed class Witness<T> : IWitness<T>
{
    public Meter Meter { get; }
    public ActivitySource ActivitySource { get; }
    public ILogger<T> Logger { get; }

    ILogger IWitness.Logger => Logger;

    public Witness(Meter meter, ActivitySource activitySource, ILogger<T> logger)
    {
        Meter = meter;
        ActivitySource = activitySource;
        Logger = logger;
    }
}
