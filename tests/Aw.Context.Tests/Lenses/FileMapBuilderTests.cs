using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Lenses;

public sealed class FileMapBuilderTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    private static readonly string[] _files =
    [
        "README.md",
        "src/billing/Invoice.ts",
        "src/billing/Tax.ts",
        "src/layout/Pane.ts",
        "src/tabs/TabGroups.ts",
        "src/tabs/TabManager.ts",
        "src/tabs/Util.ts",
    ];

    private static GraphSnapshot Graph() => new GraphBuilder()
        .Node("feature:tabs.tab-manager").Node("feature:tabs.tab-groups").Node("feature:layout")
        .Edge("code:src/tabs/TabManager.ts", EdgeType.Implements, "feature:tabs.tab-manager", provenance: Provenance.Deterministic)
        .Edge("code:src/tabs/TabGroups.ts", EdgeType.Implements, "feature:tabs.tab-groups", provenance: Provenance.Model, confidence: 0.74)
        .Edge("code:src/layout/Pane.ts", EdgeType.Implements, "feature:layout")
        .Build();

    private static FileMapView Map(string path = "src/", bool unmappedOnly = false, string[]? files = null, GraphSnapshot? graph = null) =>
        FileMapBuilder.Build("v13@7c3e1a2", new FileMapQuery(_repo, path, unmappedOnly, Draft: false), files ?? _files, graph ?? Graph()).Value;

    private static string[] Shape(FileMapView view) => [.. view.Entries.Select(e => $"{e.Path} {e.Status.ToString().ToLowerInvariant()} {e.Files.ToString(CultureInfo.InvariantCulture)}")];

    [Fact, Trait("Lane", "unit")]
    public void A_directory_lists_its_direct_children_with_the_worst_status_of_what_is_under_them()
    {
        var view = Map();

        Assert.Equal(["src/billing/ unmapped 2", "src/layout/ mapped 1", "src/tabs/ unmapped 3"], Shape(view));
        Assert.All(view.Entries, e => Assert.True(e.Directory));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_subdirectory_lists_its_files_by_their_own_status()
    {
        var view = Map("src/tabs");

        Assert.Equal(["src/tabs/TabGroups.ts suggested 1", "src/tabs/TabManager.ts mapped 1", "src/tabs/Util.ts unmapped 1"], Shape(view));
        Assert.All(view.Entries, e => Assert.False(e.Directory));
        Assert.Equal("TabManager.ts", view.Entries[1].Name);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_directory_whose_files_are_only_suggested_or_mapped_is_suggested()
    {
        var view = Map("src/", files: ["src/tabs/TabGroups.ts", "src/tabs/TabManager.ts"]);

        Assert.Equal(["src/tabs/ suggested 2"], Shape(view));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_root_lists_directories_before_files()
    {
        var view = Map("");

        Assert.Equal(["src/ unmapped 6", "README.md unmapped 1"], Shape(view));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_names_the_features_its_files_implement()
    {
        var view = Map("src/tabs");

        Assert.Equal(["feature:tabs.tab-manager"], view.Entries.Single(e => e.Name == "TabManager.ts").Features.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Equal(["feature:tabs.tab-groups"], view.Entries.Single(e => e.Name == "TabGroups.ts").Features.Select(k => k.Value), StringComparer.Ordinal);
        Assert.Empty(view.Entries.Single(e => e.Name == "Util.ts").Features);
        Assert.Equal(["feature:tabs.tab-groups", "feature:tabs.tab-manager"], Map().Entries.Single(e => e.Name == "tabs").Features.Select(k => k.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_totals_count_every_file_under_the_path()
    {
        Assert.Equal(new FileMapTotals(Mapped: 2, Suggested: 1, Unmapped: 3), Map().Totals);
        Assert.Equal(new FileMapTotals(2, 1, 4), Map("").Totals);
    }

    [Fact, Trait("Lane", "unit")]
    public void Suggested_links_under_the_path_come_with_their_ids()
    {
        var suggested = Assert.Single(Map().Suggested);

        var edge = new GraphEdge(NodeKey.From("code:src/tabs/TabGroups.ts"), NodeKey.From("feature:tabs.tab-groups"), EdgeType.Implements);
        Assert.Equal((LinkIds.For(edge), "code:src/tabs/TabGroups.ts", "feature:tabs.tab-groups", 0.74), (suggested.Link, suggested.File.Value, suggested.Feature.Value, suggested.Confidence));
        Assert.Empty(Map("src/billing").Suggested);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unmapped_only_keeps_unmapped_and_suggested_entries()
    {
        var view = Map("src/tabs", unmappedOnly: true);

        Assert.Equal(["src/tabs/TabGroups.ts suggested 1", "src/tabs/Util.ts unmapped 1"], Shape(view));
        Assert.Equal(3, view.Totals.Mapped + view.Totals.Suggested + view.Totals.Unmapped);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_path_with_no_file_under_it_is_003()
    {
        Assert.Equal(ContextErrors.UnknownAnchor.Code, FileMapBuilder.Build("v1@a", new FileMapQuery(_repo, "src/nowhere/", UnmappedOnly: false, Draft: false), _files, Graph()).Error.Code);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, FileMapBuilder.Build("v1@a", new FileMapQuery(_repo, "src/tabs/Util.ts", UnmappedOnly: false, Draft: false), _files, Graph()).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_view_echoes_the_query_and_never_has_symbols()
    {
        var view = FileMapBuilder.Build("v14@abc", new FileMapQuery(_repo, "src", UnmappedOnly: false, Draft: true), _files, Graph()).Value;

        Assert.Equal(("v14@abc", true, "aw-split-panes", "src/"), (view.Map, view.Draft, view.Repo.Value, view.Path));
        Assert.Equal(("unavailable", "index not built"), (view.Symbols.Status, view.Symbols.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_with_a_confirmed_and_a_suggested_edge_is_mapped_and_still_lists_the_suggestion()
    {
        var graph = Graph() with { Edges = [.. Graph().Edges, new GraphEdge(NodeKey.From("code:src/tabs/TabManager.ts"), NodeKey.From("feature:tabs.tab-groups"), EdgeType.Implements, Provenance.Model, 0.5)] };

        var view = Map("src/tabs", graph: graph);

        Assert.Equal(FileMapStatus.Mapped, view.Entries.Single(e => e.Name == "TabManager.ts").Status);
        Assert.Equal(2, view.Suggested.Count);
    }
}
