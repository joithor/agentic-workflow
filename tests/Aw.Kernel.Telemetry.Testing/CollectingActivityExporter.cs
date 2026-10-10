using System.Diagnostics;
using OpenTelemetry;

namespace Aw.Kernel.Telemetry.Testing;

/// <summary>A span exporter for tests: keeps every exported span. Pass it as <see cref="TelemetryOptions.TraceExporter"/>.</summary>
public sealed class CollectingActivityExporter : BaseExporter<Activity>
{
    private readonly Lock _gate = new();
    private readonly List<Activity> _exported = [];

    /// <summary>The spans exported so far, in order.</summary>
    public IReadOnlyList<Activity> Exported
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
    public override ExportResult Export(in Batch<Activity> batch)
    {
        lock (_gate)
        {
            foreach (var activity in batch)
            {
                _exported.Add(activity);
            }
        }

        return ExportResult.Success;
    }
}
