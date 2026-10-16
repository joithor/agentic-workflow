using System.Diagnostics.Metrics;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Host.Server;

/// <summary>The event stream's instrument under 'Aw.Sindri' (API spec §9 Telemetry): the streams open now, by caller class.</summary>
internal static class StreamTelemetry
{
    private static readonly Meter _meter = new("Aw.Sindri");

    private static readonly Gauge<int> _streams = _meter.CreateGauge<int>("sindri.streams", unit: "{stream}", description: "Event streams open now, by caller class");

    /// <summary>Records how many streams <paramref name="caller"/>'s class holds now.</summary>
    /// <param name="caller">The caller class.</param>
    /// <param name="open">The count.</param>
    internal static void Opened(CallerClass caller, int open) =>
        _streams.Record(open, new KeyValuePair<string, object?>("caller", caller.ToString().ToLowerInvariant()));
}
