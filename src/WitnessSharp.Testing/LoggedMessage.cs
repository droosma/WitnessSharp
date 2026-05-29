using Microsoft.Extensions.Logging;

namespace WitnessSharp.Testing;

/// <summary>A message captured by a <see cref="TestWitness{T}"/>.</summary>
/// <param name="Level">The log level.</param>
/// <param name="EventId">The event id.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Exception">The associated exception, if any.</param>
public sealed record LoggedMessage(
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception);
