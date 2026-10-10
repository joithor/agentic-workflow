using System.Diagnostics;
using Aw.Kernel.Events;
using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry;

/// <summary>One category's logger. The factory's filters decide the level; this only builds the entry.</summary>
internal sealed class JsonLinesLogger(string category, JsonLinesLoggerProvider provider) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => provider.Scopes.Push(state);

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(formatter);
        var fields = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
        var message = MessageText.Render(formatter(state, exception), fields, provider.IsSecretName);
        provider.Write(new LogEntry(category, logLevel, eventId, message, exception, fields, InnermostContext(), Activity.Current));
    }

    // Scopes run outermost first, so the last context seen is the innermost.
    private EventContext? InnermostContext()
    {
        var box = new ContextBox();
        provider.Scopes.ForEachScope(static (scope, found) => found.Value = scope as EventContext ?? found.Value, box);
        return box.Value;
    }

    private sealed class ContextBox
    {
        public EventContext? Value { get; set; }
    }
}
