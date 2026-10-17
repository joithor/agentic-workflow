namespace Aw.Sindri.Features.Sessions;

/// <summary>Rebuild the set of sessions the collector last reported, from the ledger's 'fleet.changed' rows of the last 24 hours (collector spec §4.1 step 9).</summary>
public sealed record RestoreFleetHistoryRequest;
