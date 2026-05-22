using Microsoft.Extensions.Logging;

namespace WitnessSharp.Testing;

public sealed record LoggedMessage(
    LogLevel Level,
    EventId EventId,
    string Message,
    Exception? Exception);
