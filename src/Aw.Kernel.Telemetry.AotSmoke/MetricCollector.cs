using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Aw.Kernel.Telemetry.AotSmoke;

/// <summary>An opt-in metric exporter that keeps the names of the streams it is given.</summary>
internal sealed class MetricCollector : BaseExporter<Metric>
{
    public List<string> Names { get; } = [];

    public override ExportResult Export(in Batch<Metric> batch)
    {
        foreach (var metric in batch)
        {
            Names.Add(metric.Name);
        }

        return ExportResult.Success;
    }
}
