using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Ui.Testing;

/// <summary>A logger that keeps each entry's level, event, text and exception, for a test to read.</summary>
internal sealed class RecordingLogger : ILogger
{
    private readonly List<RecordedLog> _entries = [];
    private readonly Lock _gate = new();

    /// <summary>The entries so far, in order.</summary>
    public IReadOnlyList<RecordedLog> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            _entries.Add(new RecordedLog(logLevel, eventId.Name, formatter(state, exception), exception));
        }
    }
}
