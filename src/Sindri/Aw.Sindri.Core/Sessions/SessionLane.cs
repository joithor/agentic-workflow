namespace Aw.Sindri.Core.Sessions;

/// <summary>The Meta lane a session runs as (collector spec §4.3).</summary>
/// <param name="Team">The team, from AW_META_TEAM.</param>
/// <param name="Lane">The lane, from AW_META_LANE.</param>
public sealed record SessionLane(string Team, string Lane);
