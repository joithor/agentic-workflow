using Aw.Kernel;

namespace Aw.Meta.Core.Lanes;

/// <summary>Who a lane-side hook or command is: the team and lane the launcher exported into the lane's environment. Nothing else names a lane to them.</summary>
/// <param name="Team">The team name (AW_META_TEAM).</param>
/// <param name="Lane">The lane name (AW_META_LANE).</param>
public sealed record LaneIdentity(string Team, string Lane)
{
    /// <summary>The lane's session name, which is its tmux session: "team-lane".</summary>
    public string SessionName => Team + "-" + Lane;

    /// <summary>Reads the identity from the environment.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The identity, or AW-META-014 when either variable is unset or blank.</returns>
    public static Result<LaneIdentity> FromEnvironment(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment("AW_META_TEAM") is { Length: > 0 } team && !string.IsNullOrWhiteSpace(team)
            && environment("AW_META_LANE") is { Length: > 0 } lane && !string.IsNullOrWhiteSpace(lane)
            ? new LaneIdentity(team.Trim(), lane.Trim())
            : MetaErrors.LaneVariablesUnset.With("set by scripts/meta-lane.sh in every lane's environment");
    }
}
