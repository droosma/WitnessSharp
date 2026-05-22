using System.Diagnostics.Metrics;

namespace WitnessSharp;

public static class WitnessMetricExtensions
{
    public static Counter<T> Counter<T>(this IWitness witness, string name) where T : struct
        => witness.Meter.CreateCounter<T>(name);

    public static Histogram<T> Histogram<T>(this IWitness witness, string name) where T : struct
        => witness.Meter.CreateHistogram<T>(name);

    public static UpDownCounter<T> UpDownCounter<T>(this IWitness witness, string name) where T : struct
        => witness.Meter.CreateUpDownCounter<T>(name);

    public static Gauge<T> Gauge<T>(this IWitness witness, string name) where T : struct
        => witness.Meter.CreateGauge<T>(name);
}
