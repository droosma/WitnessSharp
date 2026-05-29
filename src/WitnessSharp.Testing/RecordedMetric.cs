namespace WitnessSharp.Testing;

/// <summary>A metric measurement captured by a <see cref="TestWitness{T}"/>.</summary>
/// <param name="InstrumentName">The instrument name.</param>
/// <param name="Value">The recorded measurement value.</param>
/// <param name="Tags">The tags attached to the measurement.</param>
public sealed record RecordedMetric(
    string InstrumentName,
    object? Value,
    IReadOnlyList<KeyValuePair<string, object?>> Tags);
