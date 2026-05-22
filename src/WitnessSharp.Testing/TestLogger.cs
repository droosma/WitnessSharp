using Microsoft.Extensions.Logging;

namespace WitnessSharp.Testing;

internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly List<LoggedMessage> _messages = [];

    public IReadOnlyList<LoggedMessage> Messages => _messages;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _messages.Add(new LoggedMessage(logLevel, eventId, formatter(state, exception), exception));
    }
}
