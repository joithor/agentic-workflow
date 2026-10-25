using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.MapStatus;

public sealed class MapStatusTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_map_status_is_SND_CONTEXT_030_with_its_fix()
    {
        var status = await _world.Map.MapStatus(anchor: null, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.NoMap.Code, status.Error.Code);
        Assert.Contains("fix: sindri context map import <file>", status.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_human_sees_the_current_version_its_counts_and_the_retained_ones()
    {
        _ = await _world.ImportFixture();
        _ = await _world.ImportFixture();

        var status = (await _world.Map.MapStatus(anchor: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal((2, MapVersionStatus.Current), (status.Version.Number, status.Version.Status));
        Assert.Equal(new MapCounts(20, 21, 1, 1, 2), status.Counts);
        Assert.Equal((0, 1), (status.Orphaned, status.Retained));
        Assert.Null(status.ScopedTo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_sees_only_its_repos_counts()
    {
        _ = await _world.ImportFixture();

        var status = (await _world.Map.MapStatus(anchor: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None)).Value;

        Assert.Equal(new MapCounts(FixtureMap.ScopedNodes.Count, FixtureMap.ScopedEdges, 1, 0, 1), status.Counts);
        Assert.Equal(FixtureMap.Repo, status.ScopedTo?.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("other", true, "SND-CONTEXT-003")]
    [InlineData("aw-split-panes", false, "SND-CONTEXT-020")]
    public async Task A_session_outside_the_map_or_with_sessions_off_is_refused(string repo, bool enabled, string code)
    {
        _ = await _world.ImportFixture();

        var status = await _world.Map.MapStatus(anchor: null, MapWorld.Session(repo, enabled), MapWorld.None);

        Assert.Equal(code, status.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Sessions_off_is_SND_CONTEXT_020_even_without_a_map()
    {
        var status = await _world.Map.MapStatus(anchor: null, MapWorld.Session(FixtureMap.Repo, enabled: false), MapWorld.None);

        Assert.Equal(ContextErrors.SessionsDisabled.Code, status.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Map_status_coverage_for_an_anchor_equals_the_lenses_coverage()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);

        var status = (await _world.Map.MapStatus("feature:tabs.tab-manager", MapWorld.Human, MapWorld.None)).Value;
        var lenses = (await _world.Engine.Lenses("feature:tabs.tab-manager", read: MapRead.Current, depth: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.NotNull(status.Coverage);
        Assert.Equivalent(lenses.Coverage with { Map = null }, status.Coverage);
        Assert.Equal((6, 2, 6, 2), (status.Coverage.Scope, status.Coverage.Intent.Covered, status.Coverage.Product.Confirmed, status.Coverage.Code.Implemented));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_an_anchor_there_is_no_coverage_and_an_unknown_anchor_is_003()
    {
        _ = await _world.ImportYaml(MapWorld.TabsMap);

        var plain = (await _world.Map.MapStatus(anchor: null, MapWorld.Human, MapWorld.None)).Value;
        var unknown = await _world.Map.MapStatus("feature:no-such", MapWorld.Human, MapWorld.None);

        Assert.Null(plain.Coverage);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, unknown.Error.Code);
    }
}
