namespace Aw.Sindri.Core.Sessions;

/// <summary>A Meta team's name and lane names, the only parts of its team.json host detection reads (collector spec §6.1).</summary>
/// <param name="Team">The team name.</param>
/// <param name="Lanes">The lane names.</param>
public sealed record TeamNames(string Team, IReadOnlyList<string> Lanes);
