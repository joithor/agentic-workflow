using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Lenses;

/// <summary>The tabs map ('Fixtures/map-tabs.json') the S-4 lenses and file-map tests read: what it must hold.</summary>
public sealed class MapTabsFixtureTests
{
    private static readonly string _path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "map-tabs.json");

    private static MapImport Load() => Maps.Checked(Maps.Parse(File.ReadAllText(_path)));

    [Fact, Trait("Lane", "unit")]
    public void It_passes_the_map_file_checks_for_the_split_panes_repo()
    {
        var map = Load();

        Assert.Equal("aw-split-panes", map.Repo.Value);
        Assert.Contains(map.Graph.Nodes, n => n.Key.Value == "feature:tabs.tab-manager");
    }

    [Fact, Trait("Lane", "unit")]
    public void It_holds_one_supersedes_conflict()
    {
        var supersedes = Load().Graph.Edges.Where(e => e.Type == EdgeType.Supersedes).ToList();

        _ = Assert.Single(supersedes);
    }

    [Fact, Trait("Lane", "unit")]
    public void It_holds_one_orphaned_override()
    {
        var map = Load();

        var applied = OverrideApplier.Apply(map.Graph, map.Overrides);

        _ = Assert.Single(applied.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void It_holds_one_model_implements_link_at_0_74()
    {
        var suggested = Load().Graph.Edges.Where(e => e.Type == EdgeType.Implements && e.Provenance == Provenance.Model).ToList();

        var link = Assert.Single(suggested);
        Assert.Equal(0.74, link.Confidence);
        Assert.Equal("code:src/tabs/TabGroups.ts", link.From.Value);
    }
}
