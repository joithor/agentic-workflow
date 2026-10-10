using System.Diagnostics.CodeAnalysis;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// Wraps the host's metric exporter. A metric's points can't be rewritten, so an instrument with any point whose tag
/// carries a secret (by value or by name), or whose scrub was blocked or failed, is withheld from every export for the
/// life of the process (cumulative points come back each time) and counted once. Metric tags should be small fixed
/// sets; a secret there is a bug, and it never leaves the process.
/// </summary>
/// <remarks>
/// The wrapped exporter's ParentProvider stays unset (its setter is internal to OpenTelemetry), so an exporter that
/// reads the resource from it sees none: docs/csharp/telemetry.md.
/// </remarks>
[ExportModes(ExportModes.Push)]
internal sealed class ScrubbingMetricExporter(BaseExporter<Metric> inner, Scrubber scrubber, Withheld withheld) : BaseExporter<Metric>
{
    private readonly HashSet<string> _withheld = new(StringComparer.Ordinal);

    public override ExportResult Export(in Batch<Metric> batch)
    {
        var kept = new List<Metric>();
        foreach (var metric in batch)
        {
            var id = metric.MeterName + "/" + metric.Name;
            if (!_withheld.Contains(id) && IsClean(metric))
            {
                kept.Add(metric);
            }
            else if (_withheld.Add(id))
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

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A tag value whose text throws makes the instrument unexportable; it is withheld and counted, never thrown into the metric reader.")]
    private bool IsClean(Metric metric)
    {
        var tally = default(ScrubTally);
        try
        {
            foreach (ref readonly var point in metric.GetMetricPoints())
            {
                foreach (var tag in point.Tags)
                {
                    _ = scrubber.ScrubValue(tag.Key, tag.Value, ref tally);
                }
            }
        }
        catch (Exception)
        {
            withheld.Failure();
            return false;
        }

        return tally.Clean;
    }
}
