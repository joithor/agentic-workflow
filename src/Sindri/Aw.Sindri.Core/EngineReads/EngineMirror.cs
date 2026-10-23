namespace Aw.Sindri.Core.EngineReads;

/// <summary>How far the ledger side of the engine is behind (API spec §3.8, E-30).</summary>
/// <param name="LagEvents">The ledger rows the event bridge has not delivered yet.</param>
/// <param name="LagMs">How long the oldest of them has waited, in milliseconds.</param>
public sealed record EngineMirror(long LagEvents, long LagMs);
