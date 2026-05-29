using System.Diagnostics.Metrics;

namespace WitnessSharp;

/// <summary>Ergonomic extension methods on <see cref="IWitness"/> for creating metric instruments.</summary>
public static class WitnessMetricExtensions
{
    /// <summary>Creates a <see cref="Counter{T}"/> on the witness's meter.</summary>
    /// <typeparam name="T">The numeric measurement type.</typeparam>
    /// <param name="witness">The witness whose meter is used.</param>
    /// <param name="name">The instrument name.</param>
    /// <returns>The created counter.</returns>
    public static Counter<T> Counter<T>(this IWitness witness, string name) where T : struct
        => witness.Meter.CreateCounter<T>(name);

    /// <summary>Creates a <see cref="Histogram{T}"/> on the witness's meter.</summary>
    /// <typeparam name="T">The numeric measurement type.</typeparam>
    /// <param name="witness">The witness whose meter is used.</param>
    /// <param name="name">The instrument name.</param>
    /// <returns>The created histogram.</returns>
    public static Histogram<T> Histogram<T>(this IWitness witness, string name) where T : struct
        => witness.Meter.CreateHistogram<T>(name);

    /// <summary>Creates an <see cref="UpDownCounter{T}"/> on the witness's meter.</summary>
    /// <typeparam name="T">The numeric measurement type.</typeparam>
    /// <param name="witness">The witness whose meter is used.</param>
    /// <param name="name">The instrument name.</param>
    /// <returns>The created up-down counter.</returns>
    public static UpDownCounter<T> UpDownCounter<T>(this IWitness witness, string name) where T : struct
        => witness.Meter.CreateUpDownCounter<T>(name);

    /// <summary>Creates a <see cref="Gauge{T}"/> on the witness's meter.</summary>
    /// <typeparam name="T">The numeric measurement type.</typeparam>
    /// <param name="witness">The witness whose meter is used.</param>
    /// <param name="name">The instrument name.</param>
    /// <returns>The created gauge.</returns>
    public static Gauge<T> Gauge<T>(this IWitness witness, string name) where T : struct
        => witness.Meter.CreateGauge<T>(name);
}
