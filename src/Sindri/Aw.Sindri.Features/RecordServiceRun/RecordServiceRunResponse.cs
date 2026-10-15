namespace Aw.Sindri.Features.RecordServiceRun;

/// <summary>Where the run landed.</summary>
/// <param name="Sequence">Its ledger position.</param>
public sealed record RecordServiceRunResponse(long Sequence);
