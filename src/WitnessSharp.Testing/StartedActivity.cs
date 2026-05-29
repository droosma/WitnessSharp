using System.Diagnostics;

namespace WitnessSharp.Testing;

/// <summary>An activity captured by a <see cref="TestWitness{T}"/> when it stops.</summary>
/// <param name="Name">The activity display name.</param>
/// <param name="Tags">The tags set on the activity.</param>
/// <param name="Status">The final activity status.</param>
public sealed record StartedActivity(
    string Name,
    IReadOnlyList<KeyValuePair<string, object?>> Tags,
    ActivityStatusCode Status);
