using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Review;

namespace Aw.Context.Tests.Review;

public sealed class ReviewFileMatcherTests
{
    private static ReviewQueueView Queue(params MapOverride[] overrides) => ReviewBacklog.Build("v14@7c3e1a2", SampleDrafts.Draft(), overrides);

    private static ReviewFile Edit(ReviewQueueView queue, Func<ReviewFileEntry, ReviewFileEntry> edit) =>
        ReviewFile.Of(queue) with { Entries = [.. ReviewFile.Of(queue).Entries.Select(edit)] };

    private static ReviewFileEntry Decide(ReviewFileEntry e, string node, ReviewVerb verb, NodeKey? other = null) =>
        e.Node?.Value == node ? e with { Decision = verb, Other = other } : e;

    [Fact, Trait("Lane", "unit")]
    public void A_decided_entry_that_still_sits_in_the_queue_becomes_a_decision()
    {
        var queue = Queue();
        var file = Edit(queue, e => Decide(e, "feature:layout", ReviewVerb.Reject));

        var match = ReviewFileMatcher.Match(file, queue).Value;

        var decision = Assert.Single(match.Decisions);
        Assert.Equal((ReviewVerb.Reject, "feature:layout"), (decision.Verb, decision.Node?.Value));
        Assert.Empty(match.Skipped);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_entry_becomes_a_link_decision()
    {
        var queue = Queue();
        var link = SampleDrafts.Link("code:src/layout/Panes.ts", EdgeType.Implements, "feature:layout");
        var file = Edit(queue, e => e.Link?.Value == link.Value ? e with { Decision = ReviewVerb.Accept } : e);

        var decision = Assert.Single(ReviewFileMatcher.Match(file, queue).Value.Decisions);

        Assert.Equal((ReviewVerb.Accept, link.Value, null), (decision.Verb, decision.Link?.Value, decision.Node?.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void Undecided_entries_are_ignored()
    {
        var queue = Queue();
        var file = Edit(queue, e => Decide(e, "feature:layout", ReviewVerb.Accept));

        _ = Assert.Single(ReviewFileMatcher.Match(file, queue).Value.Decisions);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_entry_the_draft_no_longer_queues_is_skipped_and_the_rest_applied()
    {
        var queue = Queue();
        var file = Edit(queue, e => Decide(Decide(e, "feature:layout", ReviewVerb.Accept), "surface:command/close-tab", ReviewVerb.Accept));
        var later = Queue(SampleDrafts.Row(OverrideAction.Reject, "surface:command/close-tab"));

        var match = ReviewFileMatcher.Match(file, later).Value;

        Assert.Equal(["feature:layout"], match.Decisions.Select(d => d.Node?.Value), StringComparer.Ordinal);
        var skipped = Assert.Single(match.Skipped);
        Assert.StartsWith("surface:command/close-tab", skipped, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void When_no_decided_entry_matches_it_is_043()
    {
        var queue = Queue();
        var file = Edit(queue, e => Decide(e, "feature:layout", ReviewVerb.Accept));

        var none = ReviewFileMatcher.Match(file, Queue(SampleDrafts.Row(OverrideAction.Reject, "feature:layout")));
        var nothing = ReviewFileMatcher.Match(ReviewFile.Of(queue), queue);

        Assert.Equal(ContextErrors.ReviewFileNoMatch.Code, none.Error.Code);
        Assert.Equal(ContextErrors.ReviewFileNoMatch.Code, nothing.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rename_takes_the_entrys_label_and_a_move_or_merge_needs_its_other_node()
    {
        var queue = Queue();
        var renamed = Edit(queue, e => e.Node?.Value == "feature:layout" ? e with { Decision = ReviewVerb.Rename, Label = "Panes" } : e);
        var moved = Edit(queue, e => Decide(e, "feature:layout", ReviewVerb.Move, SampleDrafts.Key("feature:tabs")));
        var moveless = Edit(queue, e => Decide(e, "feature:layout", ReviewVerb.Move));

        Assert.Equal("Panes", ReviewFileMatcher.Match(renamed, queue).Value.Decisions.Single().Label);
        Assert.Equal("feature:tabs", ReviewFileMatcher.Match(moved, queue).Value.Decisions.Single().Other?.Value);
        Assert.Equal(ContextErrors.ReviewFileNoMatch.Code, ReviewFileMatcher.Match(moveless, queue).Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(ReviewVerb.Undo)]
    [InlineData(ReviewVerb.Drop)]
    [InlineData(ReviewVerb.AcceptDeterministic)]
    public void A_verb_a_file_cannot_carry_is_skipped_with_a_reason(ReviewVerb verb)
    {
        var queue = Queue();
        var file = Edit(queue, e => Decide(Decide(e, "surface:command/close-tab", verb), "feature:layout", ReviewVerb.Accept));

        var match = ReviewFileMatcher.Match(file, queue).Value;

        Assert.Equal(["feature:layout"], match.Decisions.Select(d => d.Node?.Value), StringComparer.Ordinal);
        Assert.Contains("can't carry", Assert.Single(match.Skipped), StringComparison.Ordinal);
    }
}
