using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The interactive review loop as a state machine (the TTY shell only reads lines and sends what it says).</summary>
public sealed class ReviewSessionTests
{
    private static readonly OverrideId _orphan = OverrideId.From("ovr_0123456789abcdef");

    private static ReviewEntry Entry(string node, ReviewGroup group, string label, LinkId? link = null, OverrideId? orphan = null) =>
        new(NodeKey.From(node), link, group, label, group == ReviewGroup.Deterministic ? Provenance.Deterministic : Provenance.Model, 0.7, 1, [NodeKey.From("code:src/a.ts")], Parent: null, orphan);

    private static ReviewQueueView Queue() => ReviewQueueView.Of("v14@0a1b2c3",
    [
        Entry("surface:command/close-tab", ReviewGroup.Deterministic, "Close tab"),
        Entry("surface:command/switch-tab", ReviewGroup.Deterministic, "Switch tab"),
        Entry("feature:layout", ReviewGroup.Model, "Layout"),
        Entry("feature:groups", ReviewGroup.Model, "Tab groups"),
        Entry("feature:zoom", ReviewGroup.Model, "Zoom"),
        Entry("surface:command/long-gone", ReviewGroup.Orphaned, "accept long-gone", orphan: _orphan),
    ], decided: 0);

    [Fact, Trait("Lane", "unit")]
    public void It_starts_by_offering_the_bulk_accept_of_the_deterministic_proposals_with_their_count()
    {
        var session = new ReviewSession(Queue());

        var first = session.Start();

        Assert.Contains("2 deterministic", first.Prompt, StringComparison.Ordinal);
        Assert.Null(first.Send);
    }

    [Fact, Trait("Lane", "unit")]
    public void Yes_sends_the_bulk_accept_then_each_item_is_accepted_rejected_or_skipped_and_quit_keeps_what_was_decided()
    {
        var session = new ReviewSession(Queue());
        _ = session.Start();

        var bulk = session.Answer("y");
        var accept = session.Answer("a");
        var reject = session.Answer("r");
        var skip = session.Answer("s");
        var quit = session.Answer("q");

        Assert.Equal("accept-deterministic", bulk.Send!.Verb);
        Assert.Contains("Layout", bulk.Prompt, StringComparison.Ordinal);
        Assert.Equal(("accept", "feature:layout"), (accept.Send!.Verb, accept.Send.Node));
        Assert.Equal(("reject", "feature:groups"), (reject.Send!.Verb, reject.Send.Node));
        Assert.Null(skip.Send);
        Assert.Null(quit.Prompt);
        Assert.Equal(new ReviewCounts(Accepted: 3, Rejected: 1, Edited: 0, Skipped: 1, Dropped: 0), session.Counts);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_to_the_bulk_accept_lists_the_deterministic_items_one_by_one()
    {
        var session = new ReviewSession(Queue());
        _ = session.Start();

        var step = session.Answer("n");

        Assert.Null(step.Send);
        Assert.Contains("Close tab", step.Prompt, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Rename_and_move_ask_for_their_value_first_and_an_orphan_is_dropped_or_repointed()
    {
        var session = new ReviewSession(Queue());
        _ = session.Start();
        _ = session.Answer("y");

        var ask = session.Answer("n");
        var rename = session.Answer("Pane layout");
        var askMove = session.Answer("m");
        var move = session.Answer("feature:tabs");
        _ = session.Answer("s");
        var orphan = session.Answer("p");
        var repoint = session.Answer("feature:zoom");

        Assert.Null(ask.Send);
        Assert.Contains("new name", ask.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(("rename", "feature:layout", "Pane layout"), (rename.Send!.Verb, rename.Send.Node, rename.Send.Label));
        Assert.Null(askMove.Send);
        Assert.Equal(("move", "feature:groups", "feature:tabs"), (move.Send!.Verb, move.Send.Node, move.Send.Other));
        Assert.Contains("node", orphan.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(("repoint", _orphan.Value, "feature:zoom"), (repoint.Send!.Verb, repoint.Send.Override, repoint.Send.Node));
        Assert.Equal(new ReviewCounts(Accepted: 2, Rejected: 0, Edited: 2, Skipped: 1, Dropped: 1), session.Counts);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_key_asks_again_and_the_end_of_the_queue_ends_the_loop()
    {
        var session = new ReviewSession(ReviewQueueView.Of("v14@0a1b2c3", [Entry("feature:layout", ReviewGroup.Model, "Layout")], 0));
        _ = session.Start();

        var again = session.Answer("zzz");
        var done = session.Answer("a");

        Assert.Null(again.Send);
        Assert.Contains("Layout", again.Prompt, StringComparison.Ordinal);
        Assert.Equal("accept", done.Send!.Verb);
        Assert.Null(done.Prompt);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_entry_is_decided_by_its_link_id_and_an_empty_queue_has_nothing_to_ask()
    {
        var link = LinkId.From("edge:0123456789abcdef");
        var session = new ReviewSession(ReviewQueueView.Of("v14@0a1b2c3", [Entry("code:src/a.ts", ReviewGroup.Model, "src/a.ts implements Layout", link)], 0));

        _ = session.Start();
        var accept = session.Answer("a");

        Assert.Equal(("accept", link.Value, null), (accept.Send!.Verb, accept.Send.Link, accept.Send.Node));
        Assert.Null(new ReviewSession(ReviewQueueView.Of("v14@0a1b2c3", [], 0)).Start().Prompt);
    }
}
