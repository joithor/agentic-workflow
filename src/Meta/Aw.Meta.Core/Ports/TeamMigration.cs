namespace Aw.Meta.Core.Ports;

/// <summary>What <see cref="ITeamCatalog.Migrate"/> did (or would do).</summary>
/// <param name="Team">The team's name.</param>
/// <param name="From">The legacy directory.</param>
/// <param name="To">The team's new directory.</param>
/// <param name="Moved">The names moved.</param>
/// <param name="Left">The names left where they are, with why.</param>
/// <param name="DryRun">Whether nothing was moved.</param>
public sealed record TeamMigration(string Team, string From, string To, IReadOnlyList<string> Moved, IReadOnlyList<string> Left, bool DryRun);
