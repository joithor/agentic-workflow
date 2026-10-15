using System.Diagnostics.Metrics;

namespace Aw.Sindri.Host.Server;

/// <summary>The projector's instrument under 'Aw.Sindri' (API spec §9 Telemetry): how far the views trail the ledger.</summary>
internal static class ProjectorTelemetry
{
    private static readonly Meter _meter = new("Aw.Sindri");

    internal static Gauge<long> Lag { get; } =
        _meter.CreateGauge<long>("sindri.projector.lag", unit: "{event}", description: "Ledger head minus the projector's cursor");
}
