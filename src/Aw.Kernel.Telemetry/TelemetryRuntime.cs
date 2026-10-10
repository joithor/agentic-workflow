using Microsoft.Extensions.Logging;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// The running telemetry of one process: the logger factory (JSON lines through the scrubber), the tracer provider
/// (the scrubbing processor, then the exporter if one was given) and the meter provider (only when a metric exporter
/// was given). Start it once in the host and dispose it on exit, which flushes.
/// </summary>
public sealed class TelemetryRuntime : IDisposable
{
    private readonly TelemetryOptions _options;

    private TelemetryRuntime(TelemetryOptions options)
    {
        _options = options;
    }

    /// <summary>Creates loggers that write scrubbed JSON lines.</summary>
    public ILoggerFactory LoggerFactory => throw new NotSupportedException($"red: logger factory for {_options.ServiceName}");

    /// <summary>True when an exporter was given. False by default: nothing leaves the process.</summary>
    public bool IsExporting => throw new NotSupportedException($"red: exporting for {_options.ServiceName}");

    /// <summary>What the scrubber withheld so far.</summary>
    public WithheldCounts Withheld => throw new NotSupportedException($"red: withheld for {_options.ServiceName}");

    /// <summary>How many export processors run after the scrubbing processor (0 when exporting is off).</summary>
    internal int ExportProcessorCount => throw new NotSupportedException($"red: processors for {_options.ServiceName}");

    /// <summary>Starts telemetry with <paramref name="options"/>.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The runtime.</returns>
    public static TelemetryRuntime Start(TelemetryOptions options) => new(options);

    /// <summary>Exports what is buffered (spans, metrics) and flushes the log sink.</summary>
    public void Flush() => throw new NotSupportedException($"red: flush for {_options.ServiceName}");

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
