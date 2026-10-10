using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry;

/// <summary>The logger provider that writes each record as one scrubbed JSON line to the <see cref="ILogSink"/>.</summary>
internal sealed class JsonLinesLoggerProvider(ILogSink sink, Scrubber scrubber, TimeProvider clock, Withheld withheld)
    : ILoggerProvider, ISupportExternalScope
{
    public IExternalScopeProvider Scopes { get; private set; } = new LoggerExternalScopeProvider();

    public bool IsSecretName(string name) => scrubber.IsSecretName(name);

    public ILogger CreateLogger(string categoryName) => new JsonLinesLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => Scopes = scopeProvider;

    public void Write(LogEntry entry)
    {
        var tally = default(ScrubTally);
        var line = LogLineWriter.Write(entry, clock.GetUtcNow(), scrubber, ref tally);
        if (tally.Blocked)
        {
            withheld.Log();
        }

        sink.WriteLine(line);
    }

    public void Dispose() => sink.Flush();
}
