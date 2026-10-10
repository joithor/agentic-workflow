using System.Diagnostics.CodeAnalysis;
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
        sink.WriteLine(line);
        if (tally.Blocked)
        {
            withheld.Log();
        }
    }

    public void Failed() => withheld.Failure();

    /// <summary>Flushes the sink, counting (never throwing) a failure such as a writer the host already closed.</summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Flushing telemetry must never throw into the host's shutdown; a failed flush is counted.")]
    public void Flush()
    {
        try
        {
            sink.Flush();
        }
        catch (Exception)
        {
            withheld.Failure();
        }
    }

    public void Dispose() => Flush();
}
