namespace Aw.Sindri.Features.Sessions;

/// <summary>Run one collector pass (collector spec §4.1).</summary>
/// <param name="Budget">How long the sources may take; past it the pass keeps the previous snapshot and says so.</param>
public sealed record CollectSessionsRequest(TimeSpan Budget);
