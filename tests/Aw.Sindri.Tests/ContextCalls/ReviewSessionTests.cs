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
        Assert.Equal(new ReviewCounts(0, 0, 0, Skipped: 1, 0), session.Counts);   // nothing counts until the server answers
        foreach (var step in new[] { bulk, accept, reject })
        {
            _ = session.Applied(step);
        }

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
        _ = session.Applied(session.Answer("y"));

        var ask = session.Answer("n");
        var rename = session.Answer("Pane layout");
        _ = session.Applied(rename);
        var askMove = session.Answer("m");
        var move = session.Answer("feature:tabs");
        _ = session.Applied(move);
        _ = session.Applied(session.Answer("a"));
        _ = session.Answer("s");
        _ = session.Answer("s");
        var orphan = session.Answer("p");
        var repoint = session.Answer("feature:zoom");
        _ = session.Applied(repoint);

        Assert.Null(ask.Send);
        Assert.Contains("new name", ask.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(("rename", "feature:layout", "Pane layout"), (rename.Send!.Verb, rename.Send.Node, rename.Send.Label));
        Assert.Null(askMove.Send);
        Assert.Equal(("move", "feature:layout", "feature:tabs"), (move.Send!.Verb, move.Send.Node, move.Send.Other));   // a rename or a move leaves the item current until it is accepted, rejected or skipped
        Assert.Contains("node", orphan.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(("repoint", _orphan.Value, "feature:zoom"), (repoint.Send!.Verb, repoint.Send.Override, repoint.Send.Node));
        Assert.Equal(new ReviewCounts(Accepted: 3, Rejected: 0, Edited: 2, Skipped: 2, Dropped: 1), session.Counts);
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

    [Fact, Trait("Lane", "unit")]
    public void A_queue_cut_to_its_cap_offers_the_bulk_accept_of_the_whole_backlog_and_counts_it_once_the_server_answers()
    {
        var shown = ReviewQueueView.Of("v14@0a1b2c3", [Entry("surface:command/close-tab", ReviewGroup.Deterministic, "Close tab"), Entry("feature:layout", ReviewGroup.Model, "Layout")], 0);
        var capped = shown with { Counts = new QueueCounts(Deterministic: 412, Model: 1, Orphaned: 0), Truncated = new ReviewTruncated(Deterministic: true, Model: false, Orphaned: false) };
        var session = new ReviewSession(capped);

        var prompt = session.Start().Prompt;
        var bulk = session.Answer("y");
        Assert.Equal(0, session.Counts.Accepted);
        _ = session.Applied(bulk);

        Assert.Contains("412 deterministic", prompt, StringComparison.Ordinal);
        Assert.Equal(412, session.Counts.Accepted);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bulk_accept_the_server_refused_leaves_the_deterministic_items_in_the_walk_and_the_hidden_count()
    {
        var shown = ReviewQueueView.Of("v14@0a1b2c3", [Entry("surface:command/close-tab", ReviewGroup.Deterministic, "Close tab"), Entry("feature:layout", ReviewGroup.Model, "Layout")], 0);
        var capped = shown with { Counts = new QueueCounts(Deterministic: 412, Model: 1, Orphaned: 0), Truncated = new ReviewTruncated(Deterministic: true, Model: false, Orphaned: false) };
        var session = new ReviewSession(capped);
        _ = session.Start();

        var bulk = session.Answer("y");
        Assert.Contains("Layout", bulk.Prompt, StringComparison.Ordinal);
        var again = session.Failed(bulk);

        Assert.Contains("[1/2] Close tab", again.Prompt, StringComparison.Ordinal);   // the shown deterministic item is walked after all
        Assert.Equal(0, session.Counts.Accepted);
        Assert.Equal(411, session.Hidden);   // and the 411 the queue cut still wait
        Assert.Equal("accept", session.Answer("a").Send!.Verb);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_single_decision_the_server_refused_changes_nothing_in_the_session()
    {
        var session = new ReviewSession(Queue());
        _ = session.Start();
        _ = session.Answer("n");
        var accept = session.Answer("a");

        var failed = session.Failed(accept);

        Assert.Equal(0, session.Counts.Accepted);
        Assert.Equal(0, session.Counts.Skipped);   // it is not skipped either: the same item is asked again
        Assert.Contains("[1/", failed.Prompt, StringComparison.Ordinal);
        Assert.Equal(session.Answer("a").Send!.Node, accept.Send!.Node);   // the cursor stayed on the refused item
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bulk_accept_counts_the_rows_the_server_wrote_not_the_number_listed()
    {
        var session = new ReviewSession(ReviewQueueView.Of("v14@0a1b2c3", [Entry("surface:a", ReviewGroup.Deterministic, "A"), Entry("surface:b", ReviewGroup.Deterministic, "B"), Entry("surface:c", ReviewGroup.Deterministic, "C")], 0));
        _ = session.Start();
        var bulk = session.Answer("y");

        _ = session.Applied(bulk, written: 2);

        Assert.Equal(2, session.Counts.Accepted);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_session_says_how_many_undecided_items_the_queue_did_not_show()
    {
        var shown = ReviewQueueView.Of("v14@0a1b2c3", [Entry("feature:layout", ReviewGroup.Model, "Layout")], 0);
        var capped = shown with { Counts = new QueueCounts(Deterministic: 0, Model: 250, Orphaned: 0), Truncated = new ReviewTruncated(Deterministic: false, Model: true, Orphaned: false) };

        Assert.Equal(249, new ReviewSession(capped).Hidden);
        Assert.Equal(0, new ReviewSession(Queue()).Hidden);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rename_keeps_the_item_current_shows_its_new_name_only_once_the_server_agrees_and_counts_then()
    {
        var session = new ReviewSession(ReviewQueueView.Of("v14@0a1b2c3", [Entry("feature:layout", ReviewGroup.Model, "Layout"), Entry("feature:zoom", ReviewGroup.Model, "Zoom")], 0));
        _ = session.Start();
        _ = session.Answer("n");

        var rename = session.Answer("Pane layout");
        var renamed = session.Answer("n");
        var again = session.Answer("Pane layout");
        var shown = session.Applied(again);
        var accept = session.Answer("a");

        Assert.Contains("[1/2] Layout", rename.Prompt, StringComparison.Ordinal);
        Assert.Contains("new name", renamed.Prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[1/2] Pane layout", shown.Prompt, StringComparison.Ordinal);
        Assert.Equal(("accept", "feature:layout"), (accept.Send!.Verb, accept.Send.Node));
        Assert.Equal(1, session.Counts.Edited);
    }
}
