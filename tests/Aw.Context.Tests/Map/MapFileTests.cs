using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Graph;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Testing.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Map;

/// <summary>The real map file reader and exports folder on disk (integration, a temp home).</summary>
public sealed class MapFileTests : IDisposable
{
    private const string Header = "schemaVersion: 1\nkind: authored\nrepo: aw-split-panes\n";
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory, Trait("Lane", "integration")]
    [InlineData(Header + "nodes:\n  - {key: \"feature:a\", label: A}\n  - {key: \"feature:b\", label: B}\nedges:\n  - {from: \"feature:a\", to: \"feature:b\", type: 5}\n", "type")]
    [InlineData(Header + "nodes:\n  - {key: \"feature:a\", label: A, provenance: 7}\n", "provenance")]
    [InlineData(Header + "nodes:\n  - {key: \"feature:a\", label: A}\nedgse: []\n", "edgse")]
    public async Task A_number_for_a_name_or_an_unknown_member_is_SND_CONTEXT_031(string yaml, string mentioned)
    {
        var path = Path.Combine(_home.Root, "map.yaml");
        await File.WriteAllTextAsync(path, yaml, Ct);

        var read = await new MapFileReader(FixtureMap.Json).Read(path, Ct);

        Assert.Equal("SND-CONTEXT-031", read.Error.Code);
        Assert.Contains(mentioned, read.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Nesting_past_32_and_a_relative_path_are_SND_CONTEXT_031()
    {
        var deep = Path.Combine(_home.Root, "deep.yaml");
        await File.WriteAllTextAsync(deep, "nodes: " + new string('[', 5000) + new string(']', 5000) + "\n", Ct);

        var nested = await new MapFileReader(FixtureMap.Json).Read(deep, Ct);
        var relative = await new MapFileReader(FixtureMap.Json).Read("maps/product-map.yaml", Ct);

        Assert.Equal("SND-CONTEXT-031", nested.Error.Code);
        Assert.Contains("nesting is too deep", nested.Error.Message, StringComparison.Ordinal);
        Assert.Equal("SND-CONTEXT-031", relative.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_linked_exports_folder_or_map_file_inside_a_worktree_is_SND_CONTEXT_005()
    {
        var repo = Path.Combine(_home.Root, "repo");
        _ = Directory.CreateDirectory(Path.Combine(repo, ".git"));
        _ = Directory.CreateDirectory(Path.Combine(repo, "exports"));
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        _ = Directory.CreateDirectory(settings.Home);
        _ = Directory.CreateSymbolicLink(settings.ExportsDirectory, Path.Combine(repo, "exports"));
        var map = FixtureMap.WriteTo(repo);
        var linked = Path.Combine(_home.Root, "linked-map.yaml");
        _ = File.CreateSymbolicLink(linked, map);

        var written = await new FileExportStore(settings, FixtureMap.Json).Write(path: null, "x.json", new MapDocument(1, MapDocumentKind.Export, "r", []), Ct);
        var read = await new MapFileReader(FixtureMap.Json).Read(linked, Ct);

        Assert.Equal("SND-CONTEXT-005", written.Error.Code);
        Assert.Empty(Directory.GetFiles(Path.Combine(repo, "exports")));
        Assert.Equal("SND-CONTEXT-005", read.Error.Code);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_export_read_back_from_its_JSON_file_imports_to_the_same_graph()
    {
        var settings = SettingsFromEnvironment.Read(_home.Environment);
        using var database = new ContextDatabase(settings, new FakeTimeProvider());
        var store = new SqliteGraphStore(database, settings);
        var first = Maps.Checked((await new MapFileReader(FixtureMap.Json).Read(FixtureMap.WriteTo(_home.Root), Ct)).Value.Document);
        var v1 = (await store.SaveCurrent(first, SampleMap.At, Ct)).Value;
        var document = MapAccess.Export(v1, (await store.Read(v1.Number, Ct)).Value, (await store.Overrides(Ct)).Value, SampleMap.At);
        var file = (await new FileExportStore(settings, FixtureMap.Json).Write(path: null, "roundtrip.json", document, Ct)).Value;

        var again = Maps.Checked((await new MapFileReader(FixtureMap.Json).Read(file, Ct)).Value.Document);
        var v2 = (await store.SaveCurrent(again, SampleMap.At, Ct)).Value;

        Assert.Equal(Canonical.Of((await store.Read(v1.Number, Ct)).Value), Canonical.Of((await store.Read(v2.Number, Ct)).Value));
        Assert.Equal(first.Overrides.Count, (await store.Overrides(Ct)).Value.Count);
    }
}
