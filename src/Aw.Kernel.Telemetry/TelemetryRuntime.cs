using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// The running telemetry of one process: the logger factory (JSON lines through the scrubber), the tracer provider
/// (always sampling, so log lines inside a span carry its ids; with a trace exporter, the scrubbing processor and then
/// the export processor) and the meter provider (only when a metric exporter was given). Start one per process in the
/// host and dispose it on exit, which flushes. The runtime owns the exporters it was given and disposes them.
/// </summary>
public sealed class TelemetryRuntime : IDisposable
{
#pragma warning disable CA2213 // the provider is added to LoggerFactory, whose Dispose (below) disposes it
    private readonly JsonLinesLoggerProvider _logs;
#pragma warning restore CA2213
    private readonly Withheld _withheld;
    private readonly TracerProvider _tracer;
    private readonly MeterProvider? _meter;
    private int _disposed;

    private TelemetryRuntime(TelemetryOptions options, ILogSink sink, Withheld withheld)
    {
        _withheld = withheld;
        var scrubber = new Scrubber(options.Redactor);
        _logs = new JsonLinesLoggerProvider(sink, scrubber, options.Clock, withheld);
        var filter = new LoggerFilterOptions { MinLevel = options.MinimumLevel };
        filter.Rules.Add(new LoggerFilterRule(providerName: null, RequestLineCategory, options.MinimumLevel > LogLevel.Warning ? options.MinimumLevel : LogLevel.Warning, filter: null));
        foreach (var category in options.TraceCappedCategories)
        {
            filter.Rules.Add(new LoggerFilterRule(providerName: null, category, options.MinimumLevel > LogLevel.Debug ? options.MinimumLevel : LogLevel.Debug, filter: null));
        }

        var factory = new LoggerFactory([], filter);
        factory.AddProvider(_logs);
        LoggerFactory = factory;
        (_tracer, ExportProcessorCount) = BuildTracer(options, scrubber, withheld);
        _meter = options.MetricExporter is { } metricExporter ? BuildMeter(options, metricExporter, scrubber, withheld) : null;
        IsExporting = ExportProcessorCount > 0 || _meter is not null;
    }

    /// <summary>
    /// ASP.NET Core's request lines ('Request starting GET …?q=…') carry the URL with its query string, which may hold
    /// user text or secrets that no scrubber rule recognizes. This category logs at Warning and above only, in every host.
    /// </summary>
    internal const string RequestLineCategory = "Microsoft.AspNetCore.Hosting.Diagnostics";

    /// <summary>Creates loggers that write scrubbed JSON lines.</summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>True when an exporter was given. False by default: nothing leaves the process.</summary>
    public bool IsExporting { get; }

    /// <summary>What telemetry withheld so far.</summary>
    public WithheldCounts Withheld => _withheld.Counts;

    /// <summary>How many export processors the tracer provider was built with (0 when exporting is off).</summary>
    internal int ExportProcessorCount { get; }

    /// <summary>Starts telemetry with <paramref name="options"/>; writes <see cref="TelemetryOptions.Problem"/> first, if any.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The runtime.</returns>
    public static TelemetryRuntime Start(TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var runtime = new TelemetryRuntime(options, options.Sink ?? new TextWriterLogSink(Console.Error), new Withheld());
        if (options.Problem is { } problem)
        {
            runtime.LoggerFactory.CreateLogger("Aw.Kernel.Telemetry").Log(
                LogLevel.Warning, new EventId(1, problem.Code), problem.Message, exception: null, static (state, _) => state);
        }

        return runtime;
    }

    /// <summary>Exports what is buffered (spans, metrics) and flushes the log sink. Never throws; a failed flush is counted.</summary>
    public void Flush()
    {
        _ = _tracer.ForceFlush();
        _ = _meter?.ForceFlush();
        _logs.Flush();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _tracer.Dispose();
        _meter?.Dispose();
        LoggerFactory.Dispose();
    }

    private static (TracerProvider Provider, int ExportProcessors) BuildTracer(TelemetryOptions options, Scrubber scrubber, Withheld withheld)
    {
        // The sampler is set in code, so OTEL_TRACES_SAMPLER can't turn span recording (and the ids in log lines) off.
        var builder = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(options.ServiceName))
            .SetSampler(new AlwaysOnSampler())
            .AddSource(options.SourcePattern);
        if (options.TraceExporter is not { } exporter)
        {
            return (builder.Build(), 0);
        }

#pragma warning disable CA2000 // the provider builder owns the processor or reader and disposes it with the provider
        builder = builder.AddProcessor(new ScrubbingActivityProcessor(scrubber, withheld)).AddProcessor(new SimpleActivityExportProcessor(exporter));
#pragma warning restore CA2000
        return (builder.Build(), 1);
    }

    private static MeterProvider BuildMeter(TelemetryOptions options, BaseExporter<Metric> exporter, Scrubber scrubber, Withheld withheld) =>
        Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateEmpty().AddService(options.ServiceName))
            .AddMeter(options.SourcePattern, Telemetry.Withheld.MeterName)
#pragma warning disable CA2000 // the provider builder owns the processor or reader and disposes it with the provider
            .AddReader(new PeriodicExportingMetricReader(new ScrubbingMetricExporter(exporter, scrubber, withheld)))
#pragma warning restore CA2000
            .Build();
}
