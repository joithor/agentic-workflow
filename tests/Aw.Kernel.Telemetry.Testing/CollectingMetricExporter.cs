using System.Globalization;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Aw.Kernel.Telemetry.Testing;

/// <summary>A metric exporter for tests: copies every exported stream. Pass it as <see cref="TelemetryOptions.MetricExporter"/>.</summary>
public sealed class CollectingMetricExporter : BaseExporter<Metric>
{
    private readonly Lock _gate = new();
    private readonly List<ExportedMetric> _exported = [];

    /// <summary>The metric streams exported so far, in order.</summary>
    public IReadOnlyList<ExportedMetric> Exported
    {
        get
        {
            lock (_gate)
            {
                return [.. _exported];
            }
        }
    }

    /// <inheritdoc />
    public override ExportResult Export(in Batch<Metric> batch)
    {
        lock (_gate)
        {
            foreach (var metric in batch)
            {
                _exported.Add(Copy(metric));
            }
        }

        return ExportResult.Success;
    }

    private static ExportedMetric Copy(Metric metric)
    {
        var points = new List<IReadOnlyDictionary<string, string?>>();
        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            var tags = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var tag in point.Tags)
            {
                tags[tag.Key] = Convert.ToString(tag.Value, CultureInfo.InvariantCulture);
            }

            points.Add(tags);
        }

        return new ExportedMetric(metric.Name, points);
    }
}
