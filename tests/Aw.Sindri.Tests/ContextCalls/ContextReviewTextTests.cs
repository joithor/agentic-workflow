using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The words of the review, lenses and filemap verbs (UI accessibility lens): every status is a word, never colour, and an empty answer says so.</summary>
public sealed class ContextReviewTextTests
{
    private static ReviewEntry Entry(string node, ReviewGroup group, double confidence, string[] files) =>
        new(NodeKey.From(node), Link: null, group, node, group == ReviewGroup.Deterministic ? Provenance.Deterministic : Provenance.Model, confidence, 2, [.. files.Select(NodeKey.From)], Parent: null);

    [Fact, Trait("Lane", "unit")]
    public void The_queue_lists_each_item_with_its_group_provenance_confidence_and_files()
    {
        var queue = new ReviewQueueView("v14@0a1b2c3",
            [Entry("surface:command/close-tab", ReviewGroup.Deterministic, 1, ["code:src/commands/registry.ts"]), Entry("feature:layout", ReviewGroup.Model, 0.74, ["code:src/layout/SplitPane.ts"])], Decided: 3);

        var text = ContextReviewText.Queue(queue);

        Assert.Contains("draft v14@0a1b2c3", text, StringComparison.Ordinal);
        Assert.Contains("2 undecided", text, StringComparison.Ordinal);
        Assert.Contains("3 decided", text, StringComparison.Ordinal);
        Assert.Contains("deterministic", text, StringComparison.Ordinal);
        Assert.Contains("model", text, StringComparison.Ordinal);
        Assert.Contains("0.74", text, StringComparison.Ordinal);
        Assert.Contains("code:src/layout/SplitPane.ts", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_queue_says_it_is_ready_to_promote() => Assert.Contains("nothing left to decide", ContextReviewText.Queue(new ReviewQueueView("v14@0a1b2c3", [], 4)), StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void A_result_names_the_rows_written_and_what_is_left()
    {
        var text = ContextReviewText.Result("accept", new ReviewResult([OverrideId.From("ovr_0123456789abcdef")], ["one skipped: no match"], Undecided: 5));

        Assert.Contains("accept: 1 row written", text, StringComparison.Ordinal);
        Assert.Contains("5 undecided", text, StringComparison.Ordinal);
        Assert.Contains("one skipped: no match", text, StringComparison.Ordinal);
        Assert.Contains("ovr_0123456789abcdef", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_lenses_show_three_bands_a_conflict_in_words_and_the_coverage_counts()
    {
        var item = new LensItem(NodeKey.From("code:src/tabs/TabGroups.ts"), "src/tabs/TabGroups.ts", LensRelation.Own, Provenance.Model, 0.74, Confirmed: false, LinkId.From("edge:0123456789abcdef"));
        var view = new LensesView("v13", Draft: false, NodeKey.From("feature:tabs.tab-manager"),
            Intent: new LensBand([], Truncated: false), Product: new LensBand([], Truncated: false), Code: new LensBand([item], Truncated: false),
            new LensSymbols("unavailable", "index not built"), [], [new LensConflict("supersedes", NodeKey.From("decision:2"), NodeKey.From("decision:1"))],
            new LensCoverage(Scope: 6, Covered: 4, Confirmed: 3, Implemented: 2), ["no intent recorded"]);

        var text = ContextLensText.Lenses(view);

        Assert.Contains("lenses of feature:tabs.tab-manager · map v13", text, StringComparison.Ordinal);
        Assert.Contains("intent", text, StringComparison.Ordinal);
        Assert.Contains("product", text, StringComparison.Ordinal);
        Assert.Contains("code", text, StringComparison.Ordinal);
        Assert.Contains("suggested", text, StringComparison.Ordinal);
        Assert.Contains("conflict supersedes", text, StringComparison.Ordinal);
        Assert.Contains("index not built", text, StringComparison.Ordinal);
        Assert.Contains("4 of 6", text, StringComparison.Ordinal);
        Assert.Contains("no intent recorded", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_filemap_shows_each_child_with_its_status_word_and_the_suggested_links_with_their_ids()
    {
        var link = LinkId.From("edge:0123456789abcdef");
        var view = new FileMapView("v13", Draft: false, Context.Core.Engine.ContextRepoName.From("aw-split-panes"), "src/",
            [new FileMapEntry("commands", "src/commands/", Directory: true, FileMapStatus.Mapped, 2, [NodeKey.From("feature:tabs")]),
             new FileMapEntry("util.ts", "src/util.ts", Directory: false, FileMapStatus.Unmapped, 1, [])],
            [new FileMapSuggestion(link, NodeKey.From("code:src/layout/SplitPane.ts"), NodeKey.From("feature:layout"), 0.74)],
            new FileMapTotals(Mapped: 2, Suggested: 1, Unmapped: 1), new LensSymbols("unavailable", "index not built"));

        var text = ContextLensText.FileMap(view);

        Assert.Contains("aw-split-panes src/ · map v13", text, StringComparison.Ordinal);
        Assert.Contains("mapped", text, StringComparison.Ordinal);
        Assert.Contains("unmapped", text, StringComparison.Ordinal);
        Assert.Contains("suggested", text, StringComparison.Ordinal);
        Assert.Contains(link.Value, text, StringComparison.Ordinal);
        Assert.Contains("2 mapped, 1 suggested, 1 unmapped", text, StringComparison.Ordinal);
    }
}
