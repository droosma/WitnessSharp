using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace WitnessSharp;

public interface IWitness
{
    Meter Meter { get; }
    ActivitySource ActivitySource { get; }
    ILogger Logger { get; }
}

public interface IWitness<out T> : IWitness
{
    new ILogger<T> Logger { get; }
}
