using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;

namespace Aw.Context.Tests.ExportMap;

public sealed class ExportMapTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_map_export_is_SND_CONTEXT_030()
    {
        var exported = await _world.Map.ExportMap(path: null, MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.NoMap.Code, exported.Error.Code);
        Assert.Contains("context map import", exported.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Export_writes_the_current_version_under_a_default_name_in_the_exports_folder()
    {
        _ = await _world.ImportFixture();

        var exported = await _world.Map.ExportMap(path: null, MapWorld.Human, MapWorld.None);

        Assert.Equal(FakeExportStore.Folder + "/map-v1-20260801T120000Z.json", exported.Value.Path);
        Assert.Equal((1, new MapCounts(20, 21, 1, 1, 2)), (exported.Value.Version, exported.Value.Counts));
        var document = _world.Exports.Documents[exported.Value.Path];
        Assert.Equal((MapDocumentKind.Export, 1, 1), (document.Kind, document.SchemaVersion, document.MapVersion));
        Assert.Equal(_world.Clock.GetUtcNow(), document.ExportedAt);
    }

    // Golden: the export of the fixture map, as AwJson writes it (contract of the map file, spec Phase 3).
    [Fact, Trait("Lane", "unit")]
    public async Task The_fixture_export_matches_its_golden_file()
    {
        _ = await _world.ImportFixture();

        var exported = await _world.Map.ExportMap(path: null, MapWorld.Human, MapWorld.None);

        var json = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(_world.Exports.Documents[exported.Value.Path], FixtureMap.IndentedJson));
        Golden.Match(json + "\n", typeof(ExportMapTests).Assembly, "ExportMap/Golden/fixture-export.json", Normalization.None);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_export_imports_back_to_the_same_graph_with_original_timestamps_and_provenance()
    {
        var first = await _world.ImportFixture();
        var exported = await _world.Map.ExportMap(path: null, MapWorld.Human, MapWorld.None);
        var document = _world.Exports.Documents[exported.Value.Path];
        _world.Reader.Put("/maps/export.json", document, System.Text.Encoding.UTF8.GetString(AwJson.Serialize(document, FixtureMap.Json)));
        _world.Clock.Advance(TimeSpan.FromDays(1));

        var hash = (await _world.Map.PreviewMap("/maps/export.json", MapWorld.Human, MapWorld.None)).Value.Hash;
        var second = await _world.Map.ImportMap("/maps/export.json", hash, MapWorld.Human, MapWorld.None);

        Assert.Equal(first.Counts, second.Value.Counts);
        var before = Canonical.Of((await _world.Store.Read(first.Version.Number, MapWorld.None)).Value);
        Assert.Equal(before, Canonical.Of((await _world.Store.Read(second.Value.Version.Number, MapWorld.None)).Value));
        Assert.Equal(2, (await _world.Store.Overrides(MapWorld.None)).Value.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Orphaned_overrides_are_shown_in_the_export_and_survive_the_round_trip()
    {
        var withOrphan = Maps.Fixture() with
        {
            Overrides = [.. Maps.Fixture().Overrides!, new(OverrideAction.Rename, Maps.Key("feature:gone"), "avery", SampleMap.At, Label: "Gone")],
        };
        _world.Reader.Put(MapWorld.MapPath, withOrphan, "with orphan");
        _ = await _world.ImportFixture();

        var exported = await _world.Map.ExportMap(path: null, MapWorld.Human, MapWorld.None);
        var document = _world.Exports.Documents[exported.Value.Path];
        _world.Reader.Put("/maps/export.json", document, "export");
        var again = await _world.Map.ImportMap("/maps/export.json", (await _world.Map.PreviewMap("/maps/export.json", MapWorld.Human, MapWorld.None)).Value.Hash, MapWorld.Human, MapWorld.None);

        Assert.Equal("feature:gone", Assert.Single(document.Orphaned!).Value);
        Assert.True(again.IsSuccess);
        Assert.Equal(3, (await _world.Store.Overrides(MapWorld.None)).Value.Count);
        Assert.Equal(1, (await _world.Map.MapStatus(anchor: null, MapWorld.Human, MapWorld.None)).Value.Orphaned);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("../outside.json", "SND-CONTEXT-033")]
    [InlineData("/work/repo/out.json", "SND-CONTEXT-005")]
    public async Task Export_outside_the_exports_folder_is_refused_and_nothing_is_written(string path, string code)
    {
        _ = await _world.ImportFixture();

        var exported = await _world.Map.ExportMap(path, MapWorld.Human, MapWorld.None);

        Assert.Equal(code, exported.Error.Code);
        Assert.Empty(_world.Exports.Documents);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_export_holds_only_its_repos_nodes_and_the_edges_between_them()
    {
        _ = await _world.ImportFixture();

        var exported = await _world.Map.ExportMap(path: null, MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        var document = _world.Exports.Documents[exported.Value.Path];
        Assert.Equal(string.Join(',', FixtureMap.ScopedNodes), string.Join(',', document.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal)));
        Assert.Equal(FixtureMap.ScopedEdges, document.Edges!.Count);
        Assert.Empty(document.Decisions!);
        _ = Assert.Single(document.Overrides!);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("other", true, "SND-CONTEXT-003")]
    [InlineData(null, true, "SND-CONTEXT-003")]
    [InlineData("aw-split-panes", false, "SND-CONTEXT-020")]
    public async Task A_session_outside_the_map_or_with_sessions_off_is_refused(string? repo, bool enabled, string code)
    {
        _ = await _world.ImportFixture();

        var exported = await _world.Map.ExportMap(path: null, MapWorld.Session(repo, enabled), MapWorld.None);

        Assert.Equal(code, exported.Error.Code);
        Assert.Empty(_world.Exports.Documents);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Trust_is_never_in_the_file()
    {
        _ = await _world.ImportFixture();

        var exported = await _world.Map.ExportMap(path: null, MapWorld.Human, MapWorld.None);

        var json = System.Text.Encoding.UTF8.GetString(AwJson.Serialize(_world.Exports.Documents[exported.Value.Path], FixtureMap.Json));
        Assert.DoesNotContain("\"trust\"", json, StringComparison.Ordinal);
        Assert.Contains("\"provenance\":\"model\"", json, StringComparison.Ordinal);
    }
}
