using System.Diagnostics;
using OpenTelemetry;

namespace Aw.Kernel.Telemetry.AotSmoke;

/// <summary>An opt-in span exporter that keeps what it is given.</summary>
internal sealed class SpanCollector : BaseExporter<Activity>
{
    public List<Activity> Spans { get; } = [];

    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var span in batch)
        {
            Spans.Add(span);
        }

        return ExportResult.Success;
    }
}
