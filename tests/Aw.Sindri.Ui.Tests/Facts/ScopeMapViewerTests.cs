using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Facts;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Facts;

/// <summary>The scope-map viewer (the demo slice of U13): reads the API golden and the scoping schema's fact as a table, tolerantly; a body with no surfaces shows the payload tree.</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeMapViewerTests
{
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string SchemaExample => File.ReadAllText(Path.Combine(UiSource.Root, "schemas", "scoping", "examples", "scope-map.fact.valid.json"));

    private ScopeMapViewer Viewer() =>
        new(FactFixtures.Row("project-build", "fact:scope"), new FactViewerContext(_server, new FakeClipboard(), _clock, () => FactFixtures.Api, FixtureInstances.Detail("project-build").Id, _ => { }));

    private async Task<ScopeMapViewer> Loaded(string body)
    {
        _server.WorkflowAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, $$"""{"key":"fact:scope","type":"scope-map","body":{{body}},"truncated":false}""");
        var viewer = Viewer();
        await viewer.Load(Token);
        return viewer;
    }

    [Fact]
    public async Task The_API_golden_is_a_counts_line_and_a_row_per_surface()
    {
        var viewer = Viewer();

        await viewer.Load(Token);

        Assert.True(viewer.ShowsTable);
        Assert.Equal("4 surfaces · 2 mapped · 1 new · 1 unmapped", viewer.Counts);
        Assert.Equal(["sync store", "status pill", "conflict dialog", "background sync (iOS)"], viewer.Surfaces.Select(s => s.Title), StringComparer.Ordinal);
        Assert.Equal(["▣ mapped", "▣ mapped", "◇ new", "░ unmapped"], viewer.Surfaces.Select(s => s.Chip), StringComparer.Ordinal);
        Assert.Equal(["3 files", "1 file", "2 files", "3 files"], viewer.Surfaces.Select(s => s.Count), StringComparer.Ordinal);
        Assert.Empty(viewer.Sections);
    }

    [Fact]
    public async Task The_scoping_schema_fact_shows_id_kind_lens_detail_citations_and_its_sections()
    {
        var viewer = await Loaded(SchemaExample);

        var surface = viewer.Surfaces.Single();
        Assert.Equal(("Invitations", "S1 · feature · product", "Sent by email", "▣ mapped", "2 citations"), (surface.Title, surface.Meta, surface.Detail, surface.Chip, surface.Count));
        Assert.Equal([Labels.ScopeWorkstreams, Labels.ScopeFlagged, Labels.ScopeExcluded, Labels.ScopeUnresolved], viewer.Sections.Select(s => s.Heading), StringComparer.Ordinal);
        Assert.Equal(["Expiry ws-1"], viewer.Sections[0].Lines);
        Assert.Equal(["S9: no resolving citation"], viewer.Sections[1].Lines);
        Assert.Equal(["L-code-3: test helper"], viewer.Sections[2].Lines);
        Assert.Equal(["AC-2 SCP-CITE-001: the evidence cannot settle it"], viewer.Sections[3].Lines);
    }

    [Fact]
    public async Task An_unknown_graph_word_is_drawn_raw_and_counted()
    {
        var viewer = await Loaded("""{"surfaces":[{"name":"a","graph":"stale"},{"name":"b","graph":"mapped"},{"name":"c"}]}""");

        Assert.Equal(["stale", "▣ mapped", string.Empty], viewer.Surfaces.Select(s => s.Chip), StringComparer.Ordinal);
        Assert.Equal("3 surfaces · 1 mapped · 1 stale", viewer.Counts);
    }

    [Fact]
    public async Task A_body_with_no_surfaces_shows_the_payload_tree()
    {
        var viewer = await Loaded("""{"brief":"PRD.md"}""");

        Assert.Equal((false, true, false), (viewer.ShowsTable, viewer.ShowsTree, viewer.HasSurfaces));
        Assert.Equal(["brief"], viewer.Tree.Select(n => n.Key), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Show_raw_swaps_the_table_for_the_tree_and_back()
    {
        var viewer = Viewer();
        await viewer.Load(Token);

        viewer.ToggleRaw();

        Assert.Equal((false, true, Labels.ShowTable), (viewer.ShowsTable, viewer.ShowsTree, viewer.ToggleLabel));
        viewer.ToggleRaw();
        Assert.Equal((true, false, Labels.ShowRaw), (viewer.ShowsTable, viewer.ShowsTree, viewer.ToggleLabel));
    }

    [Fact]
    public async Task A_surfaces_value_that_is_not_an_array_and_odd_entries_do_not_break_the_read()
    {
        var odd = await Loaded("""{"surfaces":[1,{"name":"a","files":"x"}],"flagged":[3,"loose",{"reason":"r"}]}""");
        var notArray = await Loaded("""{"surfaces":"none"}""");

        Assert.Equal(["a"], odd.Surfaces.Select(s => s.Title), StringComparer.Ordinal);
        Assert.Equal(["loose", "r"], odd.Sections.Single().Lines);
        Assert.False(notArray.HasSurfaces);
    }

    [Fact]
    public async Task A_surface_with_no_readable_name_is_still_a_named_row_and_numbers_are_read_as_text()
    {
        var viewer = await Loaded("""{"surfaces":[{"graph":"mapped","files":[]},{"title":7,"graph":"new"}]}""");

        Assert.Equal([Labels.ScopeUnnamed, "7"], viewer.Surfaces.Select(s => s.Title), StringComparer.Ordinal);
        Assert.All(viewer.Surfaces, s => Assert.StartsWith(s.Title, s.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_missing_fact_shows_the_served_detail_and_no_api_says_so()
    {
        var offline = new ScopeMapViewer(FactFixtures.Row("project-build", "fact:scope"), new FactViewerContext(_server, new FakeClipboard(), _clock, () => null, FixtureInstances.Detail("project-build").Id, _ => { }));

        await offline.Load(Token);

        Assert.Equal((Labels.PayloadNotRunning, false, false), (offline.ProblemText, offline.ShowsTable, offline.ShowsTree));
    }
}
