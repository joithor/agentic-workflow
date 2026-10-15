namespace Aw.Meta.Features.Team;

/// <summary>The TeamInit request: write a starter team config.</summary>
/// <param name="Team">The team name; session names are "team-lane".</param>
/// <param name="Repo">The repository directory.</param>
/// <param name="Lanes">The lane names; the meta lane is added when missing.</param>
/// <param name="MetaLane">The meta lane's name.</param>
/// <param name="Log">The running log file; null for the default under the state directory.</param>
/// <param name="Force">Whether an existing config may be replaced.</param>
public sealed record TeamInitRequest(string Team, string Repo, IReadOnlyList<string> Lanes, string MetaLane, string? Log, bool Force);
