namespace Aw.Sindri.Features.Sessions;

/// <summary>What was recorded.</summary>
/// <param name="Recorded">How many events were appended (a replayed key is not counted).</param>
public sealed record RecordFleetChangesResponse(int Recorded);
