using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Search;
using Aw.Context.Core.WholeMap;
using Aw.Context.Features.SearchMap;
using Aw.Context.Infrastructure.Scrubbing;
using Aw.Context.Infrastructure.Search;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.SearchMap;

/// <summary>The search store and the document cutter over the host's real scrubbers (Aw.Redaction), not the fake: the feature "Tab manager" and the file TabManager.ts must both be found by "tab manager" in the right order.</summary>
public sealed class SearchWithRealScrubbersTests
{
    private static IReadOnlyList<string> Search(string q)
    {
        var scrubbers = new RedactionScrubberFactory(new FakeTimeProvider()).For([]).Value;
        var graph = new GraphSnapshot(
            [
                new GraphNode(NodeKey.From("code:src/tabs/TabManager.ts"), "TabManager.ts", Aw.Context.Core.Results.Provenance.Deterministic),
                new GraphNode(NodeKey.From("feature:tabs.tab-manager"), "Tab manager"),
                new GraphNode(NodeKey.From("feature:layout.split-pane"), "Split pane"),
            ],
            [], [], []);
        var docs = MapSearchDocs.Of(graph, new AreaIndex(new Dictionary<string, NodeArea>(StringComparer.Ordinal)), new MapText(scrubbers, FencePolicy.Clean));
        var index = new SqliteMapSearchIndexFactory().Build([.. docs.Select(static d => d.Doc)]).Value;
        var terms = MapSearchRules.Terms(q).Value;
        return [.. index.Search(terms, area: null, limit: 10).Value.Matches.Select(m => docs[m.Doc].Node.Key.Value)];
    }

    [Fact, Trait("Lane", "unit")]
    public void The_documents_keep_the_labels_the_real_scrubbers_clean()
    {
        var scrubbers = new RedactionScrubberFactory(new FakeTimeProvider()).For([]).Value;

        Assert.Equal("Tab manager", new MapText(scrubbers, FencePolicy.Clean).Cleaned("Tab manager", singleLine: true));
    }

    [Fact, Trait("Lane", "unit")]
    public void Tab_manager_finds_the_feature_first_over_real_scrubbers()
    {
        var hits = Search("tab manager");

        Assert.Equal("feature:tabs.tab-manager", hits[0]);
    }
}
