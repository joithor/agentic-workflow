using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// Wraps the host's metric exporter. A metric stream with a tag whose value scrubs to anything but clean (or whose name
/// is a secret's) can't be rewritten, so the whole stream is dropped from the batch and counted. Metric tags should be
/// small fixed sets; a secret there is a bug, and it never leaves the process.
/// </summary>
internal sealed class ScrubbingMetricExporter(BaseExporter<Metric> inner, Scrubber scrubber, Withheld withheld) : BaseExporter<Metric>
{
    public override ExportResult Export(in Batch<Metric> batch)
    {
        var kept = new List<Metric>();
        foreach (var metric in batch)
        {
            if (IsClean(metric))
            {
                kept.Add(metric);
            }
            else
            {
                withheld.Metric();
            }
        }

        return kept.Count == 0 ? ExportResult.Success : inner.Export(new Batch<Metric>([.. kept], kept.Count));
    }

    protected override bool OnForceFlush(int timeoutMilliseconds) => inner.ForceFlush(timeoutMilliseconds);

    protected override bool OnShutdown(int timeoutMilliseconds) => inner.Shutdown(timeoutMilliseconds);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private bool IsClean(Metric metric)
    {
        var tally = default(ScrubTally);
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            foreach (var tag in point.Tags)
            {
                _ = scrubber.ScrubField(tag.Key, tag.Value, ref tally);
            }
        }

        return tally.Clean;
    }
}
