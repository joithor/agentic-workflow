namespace Aw.Meta.Features.Health;

/// <summary>The Health request.</summary>
/// <param name="KillOrphans">Whether to SIGTERM the orphans found, by pid.</param>
public sealed record HealthRequest(bool KillOrphans);
