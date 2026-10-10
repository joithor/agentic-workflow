using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// The running telemetry of one process: the logger factory (JSON lines through the scrubber), the tracer provider
/// (the scrubbing processor, then the exporter if one was given) and the meter provider (only when a metric exporter
/// was given). Start it once in the host and dispose it on exit, which flushes. The runtime owns the exporters it was
/// given and disposes them.
/// </summary>
public sealed class TelemetryRuntime : IDisposable
{
    private readonly ILogSink _sink;
    private readonly Withheld _withheld;
    private readonly TracerProvider _tracer;
    private readonly MeterProvider? _meter;

    private TelemetryRuntime(TelemetryOptions options, ILogSink sink, Withheld withheld)
    {
        _sink = sink;
        _withheld = withheld;
        var scrubber = new Scrubber(options.Redactor);
        LoggerFactory = new LoggerFactory(
            [new JsonLinesLoggerProvider(sink, scrubber, options.Clock, withheld)],
            new LoggerFilterOptions { MinLevel = options.MinimumLevel });
        _tracer = BuildTracer(options, scrubber, withheld);
        _meter = options.MetricExporter is { } metricExporter ? BuildMeter(options, metricExporter, scrubber, withheld) : null;
        IsExporting = options.TraceExporter is not null || options.MetricExporter is not null;
        ExportProcessorCount = options.TraceExporter is null ? 0 : 1;
    }

    /// <summary>Creates loggers that write scrubbed JSON lines.</summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>True when an exporter was given. False by default: nothing leaves the process.</summary>
    public bool IsExporting { get; }

    /// <summary>What the scrubber withheld so far.</summary>
    public WithheldCounts Withheld => _withheld.Counts;

    /// <summary>How many export processors run after the scrubbing processor (0 when exporting is off).</summary>
    internal int ExportProcessorCount { get; }

    /// <summary>Starts telemetry with <paramref name="options"/>.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The runtime.</returns>
    public static TelemetryRuntime Start(TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new TelemetryRuntime(options, options.Sink ?? new TextWriterLogSink(Console.Error), new Withheld());
    }

    /// <summary>Exports what is buffered (spans, metrics) and flushes the log sink.</summary>
    public void Flush()
    {
        _ = _tracer.ForceFlush();
        _ = _meter?.ForceFlush();
        _sink.Flush();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        LoggerFactory.Dispose();
        _tracer.Dispose();
        _meter?.Dispose();
        _withheld.Dispose();
        _sink.Flush();
    }

    private static TracerProvider BuildTracer(TelemetryOptions options, Scrubber scrubber, Withheld withheld)
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(options.ServiceName))
            .AddSource(options.SourcePattern)
            .AddProcessor(new ScrubbingActivityProcessor(scrubber, withheld));
        if (options.TraceExporter is { } exporter)
        {
            builder = builder.AddProcessor(new SimpleActivityExportProcessor(exporter));
        }

        return builder.Build();
    }

    private static MeterProvider BuildMeter(TelemetryOptions options, BaseExporter<Metric> exporter, Scrubber scrubber, Withheld withheld) =>
        Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(options.ServiceName))
            .AddMeter(options.SourcePattern, Telemetry.Withheld.MeterName)
            .AddReader(new PeriodicExportingMetricReader(new ScrubbingMetricExporter(exporter, scrubber, withheld)))
            .Build();
}
