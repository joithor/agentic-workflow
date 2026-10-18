using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

public sealed class BuildMapGraphifyTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static FileCluster Cluster() => new(ClusterId.From("c1"), ["src/commands/ids.ts", "src/commands/registry.ts"], 0.7);

    private async Task<IReadOnlyList<string>> Degraded(string? section = null) =>
        (await _world.Map.BuildMap(progress: null, section is null ? MapWorld.HumanWith(MapWorld.OnlyFixtureRepo) : MapWorld.HumanWith(section), MapWorld.None)).Value.Degraded;

    [Fact, Trait("Lane", "unit")]
    public async Task An_unavailable_cluster_pass_names_its_reason_in_the_degraded_line()
    {
        _world.Clusters.Set = new ClusterSet([], Available: false, "timed out after 60 min");

        Assert.Equal(["clusters: timed out after 60 min"], await Degraded());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_truncated_snapshot_that_still_clusters_is_degraded_with_its_reason()
    {
        _world.Clusters.Set = new ClusterSet([Cluster()], Available: true, "snapshot truncated at 2048 MB (12 files skipped)");

        Assert.Equal(["clusters: snapshot truncated at 2048 MB (12 files skipped)"], await Degraded());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Clusters_with_nothing_lost_are_not_degraded()
    {
        _world.Clusters.Set = new ClusterSet([Cluster()], Available: true);

        Assert.Empty(await Degraded());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_profile_limits_reach_the_cluster_source_and_without_keys_it_gets_the_defaults()
    {
        _ = await Degraded();
        _ = await Degraded("""{"repos":["aw-split-panes"],"extract":{"graphify":{"timeoutMinutes":0,"maxSnapshotMB":512,"maxFileMB":8,"maxGraphMB":64}}}""");

        var (plain, set) = (_world.Clusters.AskedLimits[0], _world.Clusters.AskedLimits[1]);
        Assert.Equal(ClusterLimits.Default, plain);
        Assert.Equal((0, 512, 8, 64), (set.TimeoutMinutes, set.MaxSnapshotMB, set.MaxFileMB, set.MaxGraphMB));
    }
}
