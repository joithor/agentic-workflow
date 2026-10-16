using Aw.Meta.Core.Config;

namespace Aw.Meta.Testing;

/// <summary>Builds and compares team configs; the record's list members compare by reference, so the contract compares by value.</summary>
public static class TeamConfigs
{
    /// <summary>A config that uses every field.</summary>
    /// <returns>The config.</returns>
    public static TeamConfig Full() => new("sindri", "/work/repo", ["meta", "api", "ui"])
    {
        MetaLane = "meta",
        Log = "/work/log.md",
        Prompts = "/work/prompts",
        Gate = new GateConfig(
            ["repo"],
            ["**/*.md"],
            new Dictionary<string, ScopeConfig>(StringComparer.Ordinal)
            {
                ["repo"] = new(["**"], [new StepConfig("sync", ["scripts/sync.sh", "--check"])]),
                ["dotnet"] = new(["src/**"], [new StepConfig("build", ["dotnet", "build"], Heavy: true)]),
            }),
        MergeOrder = [new MergeOrderRule(Before: 190, After: 192)],
        KnownFlakes = ["SpikeEvalTests"],
        Worktrees = new WorktreeConfig(["/work/wt"], ["bin", "obj"], Enabled: false),
    };

    /// <summary>Whether two configs hold the same values.</summary>
    /// <param name="a">One config.</param>
    /// <param name="b">The other.</param>
    /// <returns>True when every field matches.</returns>
    public static bool Same(TeamConfig a, TeamConfig b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.Team == b.Team && a.Repo == b.Repo && a.MetaLane == b.MetaLane && a.Log == b.Log && a.Prompts == b.Prompts
            && a.Lanes.SequenceEqual(b.Lanes, StringComparer.Ordinal)
            && a.KnownFlakes.SequenceEqual(b.KnownFlakes, StringComparer.Ordinal)
            && a.MergeOrder.SequenceEqual(b.MergeOrder)
            && SameGate(a.Gate, b.Gate)
            && SameWorktrees(a.Worktrees, b.Worktrees);
    }

    private static bool SameWorktrees(WorktreeConfig a, WorktreeConfig b) =>
        a.Enabled == b.Enabled
        && (a.Roots ?? []).SequenceEqual(b.Roots ?? [], StringComparer.Ordinal)
        && (a.BuildOutputs ?? []).SequenceEqual(b.BuildOutputs ?? [], StringComparer.Ordinal);

    private static bool SameGate(GateConfig a, GateConfig b) =>
        a.Always.SequenceEqual(b.Always, StringComparer.Ordinal)
        && a.DocsOnly.SequenceEqual(b.DocsOnly, StringComparer.Ordinal)
        && a.Scopes.Count == b.Scopes.Count
        && a.Scopes.All(scope => b.Scopes.TryGetValue(scope.Key, out var other) && SameScope(scope.Value, other));

    private static bool SameScope(ScopeConfig a, ScopeConfig b) =>
        a.Paths.SequenceEqual(b.Paths, StringComparer.Ordinal)
        && a.Steps.Count == b.Steps.Count
        && a.Steps.Zip(b.Steps).All(pair => pair.First.Name == pair.Second.Name && pair.First.Heavy == pair.Second.Heavy
            && pair.First.Run.SequenceEqual(pair.Second.Run, StringComparer.Ordinal));
}
