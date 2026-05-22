using System.Diagnostics;

namespace WitnessSharp.Testing;

public sealed record StartedActivity(
    string Name,
    IReadOnlyList<KeyValuePair<string, object?>> Tags,
    ActivityStatusCode Status);
