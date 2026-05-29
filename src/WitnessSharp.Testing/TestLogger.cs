using Microsoft.Extensions.Logging;

namespace WitnessSharp.Testing;

internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly object _gate = new();
    private readonly List<LoggedMessage> _messages = [];

    public IReadOnlyList<LoggedMessage> Messages
    {
        get
        {
            lock (_gate)
            {
                return _messages.ToArray();
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = new LoggedMessage(logLevel, eventId, formatter(state, exception), exception);
        lock (_gate)
        {
            _messages.Add(message);
        }
    }
}
