using System.Diagnostics;
using System.Diagnostics.Metrics;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Host.Server;

/// <summary>The collector's span and metrics under 'Aw.Sindri' (collector spec §7): the pass span, the sessions gauge by provider and state, the overrun and pruned counters. Tags are small fixed sets; refs are never tags.</summary>
internal static class CollectorTelemetry
{
    internal static readonly ActivitySource Source = new("Aw.Sindri");

    private static readonly Meter _meter = new("Aw.Sindri");

    internal static Counter<long> Overruns { get; } = _meter.CreateCounter<long>("sindri.collector.overrun", unit: "{pass}", description: "Passes that took longer than their budget");

    internal static Counter<long> Pruned { get; } = _meter.CreateCounter<long>("sindri.collector.pruned", unit: "{file}", description: "Dead sessions' hook files the collector deleted");

    /// <summary>Starts the sessions gauge over <paramref name="snapshot"/>.</summary>
    internal static void Observe(ISessionSnapshot snapshot) =>
        _ = _meter.CreateObservableGauge("sindri.sessions", () => Counts(snapshot), unit: "{session}", description: "Live agent sessions by provider and state");

    private static IEnumerable<Measurement<int>> Counts(ISessionSnapshot snapshot) =>
        (snapshot.Current?.Sessions ?? []).GroupBy(static s => (s.Ref.Provider.Value, s.State))
            .Select(static g => new Measurement<int>(g.Count(), new KeyValuePair<string, object?>("provider", g.Key.Value), new KeyValuePair<string, object?>("state", g.Key.State.ToString().ToLowerInvariant())));
}
