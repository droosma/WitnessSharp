namespace WitnessSharp.Testing;

public sealed record RecordedMetric(
    string InstrumentName,
    object? Value,
    IReadOnlyList<KeyValuePair<string, object?>> Tags);
