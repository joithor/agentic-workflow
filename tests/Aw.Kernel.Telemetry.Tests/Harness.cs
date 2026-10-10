using System.Diagnostics;
using System.Text.Json;
using Aw.Kernel.Telemetry.Testing;
using Aw.Redaction;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Kernel.Telemetry.Tests;

/// <summary>A started runtime over an in-memory sink and, optionally, collecting exporters.</summary>
internal sealed class Harness : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 10, 6, 0, 0, TimeSpan.Zero);

    // One source per harness, so parallel tests never see each other's spans.
    private static int _sequence;

    public Harness(bool export = false, IRedactor? redactor = null, Microsoft.Extensions.Logging.LogLevel level = Microsoft.Extensions.Logging.LogLevel.Information)
    {
        var name = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Aw.Test.H{Interlocked.Increment(ref _sequence)}");
        Source = new ActivitySource(name);
        Meter = new System.Diagnostics.Metrics.Meter(name);
        Spans = export ? new CollectingActivityExporter() : null;
        Metrics = export ? new CollectingMetricExporter() : null;
        Runtime = TelemetryRuntime.Start(new TelemetryOptions
        {
            ServiceName = "aw-test",
            Sink = Sink,
            Clock = new FakeTimeProvider(Now),
            MinimumLevel = level,
            SourcePattern = name,
            Redactor = redactor ?? Redactor.Default,
            TraceExporter = Spans,
            MetricExporter = Metrics,
        });
    }

    public InMemoryLogSink Sink { get; } = new();

    public ActivitySource Source { get; }

    public System.Diagnostics.Metrics.Meter Meter { get; }

    public CollectingActivityExporter? Spans { get; }

    public CollectingMetricExporter? Metrics { get; }

    public TelemetryRuntime Runtime { get; }

    public Microsoft.Extensions.Logging.ILogger Logger(string category = "Aw.Test") => Runtime.LoggerFactory.CreateLogger(category);

    public IReadOnlyList<JsonElement> Lines()
    {
        Runtime.Flush();
        return [.. Sink.Lines.Select(l => JsonDocument.Parse(l).RootElement.Clone())];
    }

    public string Everything()
    {
        Runtime.Flush();
        var spans = Spans?.Exported.SelectMany(a => a.TagObjects.Select(t => $"{t.Key}={t.Value}")
            .Append(a.DisplayName).Append(a.StatusDescription ?? string.Empty)) ?? [];
        var metrics = Metrics?.Exported.SelectMany(m => m.PointTags.SelectMany(p => p.Values)) ?? [];
        return string.Join('\n', Sink.Lines.Concat(spans).Concat(metrics.Select(v => v ?? string.Empty)));
    }

    public void Dispose()
    {
        Runtime.Dispose();
        Source.Dispose();
        Meter.Dispose();
    }
}
