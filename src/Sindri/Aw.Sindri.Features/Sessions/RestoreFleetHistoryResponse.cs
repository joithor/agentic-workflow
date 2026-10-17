namespace Aw.Sindri.Features.Sessions;

/// <summary>What was restored.</summary>
/// <param name="Reported">How many sessions the ledger says were reported and not gone.</param>
public sealed record RestoreFleetHistoryResponse(int Reported);
