namespace Aw.Meta.Features.Team;

/// <summary>The TeamMigrate request: move the legacy flat layout into teams/&lt;its team name&gt;.</summary>
/// <param name="DryRun">Report what would move; move nothing.</param>
public sealed record TeamMigrateRequest(bool DryRun);
