namespace Aw.Meta.Core.Config;

/// <summary>
/// The team the meta session coordinates: everything team-specific lives here, nothing is built into the code.
/// Unknown fields in the file are ignored.
/// </summary>
/// <param name="Team">The team name; session names are "team-lane".</param>
/// <param name="Repo">The repository directory the gate and merge commands work in.</param>
/// <param name="Lanes">The lane names, the meta lane included.</param>
public sealed record TeamConfig(string Team, string Repo, IReadOnlyList<string> Lanes)
{
    /// <summary>The meta lane's name.</summary>
    public string MetaLane { get; init; } = "meta";

    /// <summary>The running log file; null means the default under the state directory.</summary>
    public string? Log { get; init; }

    /// <summary>A directory of per-lane kickoff prompts, read only by the launcher script.</summary>
    public string? Prompts { get; init; }

    /// <summary>The gate scopes.</summary>
    public GateConfig Gate { get; init; } = GateConfig.Empty;

    /// <summary>Merge-order rules.</summary>
    public IReadOnlyList<MergeOrderRule> MergeOrder { get; init; } = [];

    /// <summary>Names of tests known to be flaky.</summary>
    public IReadOnlyList<string> KnownFlakes { get; init; } = [];
}
