using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Review;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Review;

public sealed class ReviewBacklogTests
{
    private static ReviewQueueView Queue(params MapOverride[] overrides) => ReviewBacklog.Build("v14@7c3e1a2", SampleDrafts.Draft(), overrides);

    private static bool Is(ReviewEntry e, params string[] keys) => keys.Contains(e.Node.Value, StringComparer.Ordinal);

    private static string[] Shape(ReviewQueueView queue) =>
        [.. queue.Entries.Select(e => $"{e.Group.ToString().ToLowerInvariant()} {e.Link?.Value ?? e.Node.Value}")];

    [Fact, Trait("Lane", "unit")]
    public void The_queue_lists_deterministic_first_then_model_by_impact_then_orphans()
    {
        var queue = Queue(SampleDrafts.Row(OverrideAction.Rename, "feature:gone", "Gone"));
        static string Link(string from, string to) => SampleDrafts.Link(from, EdgeType.Implements, to).Value;

        Assert.Equal("v14@7c3e1a2", queue.Draft);
        Assert.Equal(
        [
            "deterministic surface:command/close-tab",
            "deterministic surface:command/switch-tab",
            "model feature:tabs.groups",
            "model " + Link("code:src/layout/Panes.ts", "feature:layout"),
            "model " + Link("code:src/tabs/GroupList.ts", "feature:tabs.groups"),
            "model " + Link("code:src/tabs/Groups.ts", "feature:tabs.groups"),
            "model feature:layout",
            "orphaned feature:gone",
        ], Shape(queue));
    }

    [Fact, Trait("Lane", "unit")]
    public void Impact_counts_the_links_that_hang_on_a_proposal()
    {
        var queue = Queue();

        Assert.Equal(3, queue.Entries.Single(e => e.Node.Value == "feature:tabs.groups" && e.Link is null).Impact);
        Assert.Equal(1, queue.Entries.Single(e => e.Node.Value == "feature:layout" && e.Link is null).Impact);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_entry_carries_its_files_parent_and_provenance()
    {
        var entry = Queue().Entries.Single(e => e.Node.Value == "feature:tabs.groups" && e.Link is null);

        Assert.Equal(["code:src/tabs/GroupList.ts", "code:src/tabs/Groups.ts"], entry.Files.Select(f => f.Value), StringComparer.Ordinal);
        Assert.Equal("feature:tabs", entry.Parent?.Value);
        Assert.Equal((ReviewGroup.Model, Provenance.Model, 0.7, "Tab groups"), (entry.Group, entry.Provenance, entry.Confidence, entry.Label));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_entry_names_the_file_and_the_feature_it_would_implement()
    {
        var entry = Queue().Entries.Single(e => e.Link is not null && e.Node.Value == "code:src/layout/Panes.ts");

        Assert.Equal(ReviewGroup.Model, entry.Group);
        Assert.Equal("feature:layout", entry.Parent?.Value);
        Assert.Equal(0.55, entry.Confidence);
        Assert.Equal(["code:src/layout/Panes.ts"], entry.Files.Select(f => f.Value), StringComparer.Ordinal);
        Assert.Contains("implements", entry.Label, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Confirmed_nodes_and_deterministic_links_are_never_queued()
    {
        var queue = Queue();

        Assert.DoesNotContain(queue.Entries, e => Is(e, "feature:tabs", "code:src/tabs/Switch.ts"));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_accept_or_a_reject_takes_the_entry_out_and_counts_as_decided()
    {
        var queue = Queue(SampleDrafts.Row(OverrideAction.Accept, "surface:command/close-tab"), SampleDrafts.Row(OverrideAction.Reject, "feature:layout"));

        Assert.DoesNotContain(queue.Entries, e => Is(e, "surface:command/close-tab", "feature:layout") && e.Link is null);
        Assert.Equal(2, queue.Decided);
        Assert.Equal(queue.Entries.Count, queue.Undecided);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reject_that_no_longer_applies_because_the_evidence_changed_is_undecided_not_decided_and_not_counted_twice()
    {
        var queue = Queue(SampleDrafts.Row(OverrideAction.Reject, "feature:layout", rejection: "an-identity-the-draft-no-longer-has"));

        Assert.Contains(queue.Entries, e => e.Node.Value == "feature:layout" && e.Link is null);
        Assert.Equal(0, queue.Decided);
        Assert.Equal(queue.Entries.Count, queue.Undecided);
        Assert.Equal(queue.Decided + queue.Counts.Deterministic + queue.Counts.Model, queue.Proposals);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rename_or_a_move_does_not_decide_a_proposal()
    {
        var queue = Queue(SampleDrafts.Row(OverrideAction.Rename, "feature:layout", "Panes"), SampleDrafts.Row(OverrideAction.Move, "feature:layout", other: "feature:tabs"));

        var entry = Assert.Single(queue.Entries, e => e.Node.Value == "feature:layout" && e.Link is null);
        Assert.Equal("Panes", entry.Label);
        Assert.Equal("feature:tabs", entry.Parent?.Value);
        Assert.Equal(0, queue.Decided);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_decision_takes_that_link_out()
    {
        var link = SampleDrafts.Link("code:src/layout/Panes.ts", EdgeType.Implements, "feature:layout");

        var queue = Queue(SampleDrafts.Row(OverrideAction.Reject, "code:src/layout/Panes.ts", link: link));

        Assert.DoesNotContain(queue.Entries, e => e.Link?.Value == link.Value);
        Assert.Equal(1, queue.Decided);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_undone_decision_puts_the_entry_back()
    {
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:layout");

        var queue = Queue(reject, SampleDrafts.Undo(reject, SampleDrafts.At.AddMinutes(1)));

        Assert.Contains(queue.Entries, e => e.Node.Value == "feature:layout" && e.Link is null);
        Assert.Equal(0, queue.Decided);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_orphan_entry_names_the_override_a_drop_would_target()
    {
        var orphan = SampleDrafts.Row(OverrideAction.Rename, "feature:gone", "Gone");

        var entry = Assert.Single(Queue(orphan).Entries, e => e.Group == ReviewGroup.Orphaned);

        Assert.Equal(orphan.ToId(), entry.Override);
        Assert.Equal("feature:gone", entry.Node.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_proposal_from_a_scoping_run_queues_as_a_model_entry_and_an_orphaned_proposal_does_not()
    {
        var run = new ProposalPayload("run-1", ContextRepoName.From("aw-split-panes"), NodeKind.Feature, SampleDrafts.Key("feature:tabs"));
        var proposal = new MapOverride(OverrideAction.Propose, SampleDrafts.Key("feature:tabs.pinning"), "service:engine", SampleDrafts.At, "Tab pinning", Proposal: run);

        var queue = Queue(proposal);

        Assert.Contains(queue.Entries, e => e.Node.Value == "feature:tabs.pinning" && e.Group == ReviewGroup.Model);
        Assert.DoesNotContain(queue.Entries, e => e.Group == ReviewGroup.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_draft_with_nothing_to_decide_has_an_empty_queue()
    {
        var queue = ReviewBacklog.Build("v1@abc", new GraphBuilder().Node("feature:a").Build(), []);

        Assert.Empty(queue.Entries);
        Assert.Equal(0, queue.Undecided);
    }

    [Fact, Trait("Lane", "unit")]
    public void Counts_split_the_undecided_entries_by_group_and_proposals_add_what_is_decided()
    {
        var queue = Queue(SampleDrafts.Row(OverrideAction.Accept, "surface:command/close-tab"), SampleDrafts.Row(OverrideAction.Rename, "feature:gone", "Gone"));

        Assert.Equal(new QueueCounts(Deterministic: 1, Model: 5, Orphaned: 1), queue.Counts);
        Assert.Equal(7, queue.Undecided);
        Assert.Equal((1, 7), (queue.Decided, queue.Proposals));
    }

    [Fact, Trait("Lane", "unit")]
    public void By_kind_counts_the_undecided_deterministic_proposals_per_node_kind()
    {
        var graph = new GraphBuilder().Node("feature:tabs")
            .Node("surface:command/a", provenance: Provenance.Deterministic).Node("surface:command/b", provenance: Provenance.Deterministic)
            .Node("action:tabs.open", provenance: Provenance.Deterministic).Node("feature:tabs.x", provenance: Provenance.Model).Build();

        var queue = ReviewBacklog.Build("v1@a", graph, []);

        Assert.Equal([("action", 1), ("surface", 2)], queue.ByKind.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => (k.Key, k.Value)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Decided_counts_only_proposals_of_this_draft_not_every_target_with_a_standing_decision()
    {
        var queue = Queue(SampleDrafts.Row(OverrideAction.Accept, "feature:tabs"), SampleDrafts.Row(OverrideAction.Accept, "feature:elsewhere"), SampleDrafts.Row(OverrideAction.Reject, "feature:layout"));

        Assert.Equal(1, queue.Decided);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_decided_scoping_proposal_counts_as_decided()
    {
        var run = new ProposalPayload("run-1", ContextRepoName.From("aw-split-panes"), NodeKind.Feature, SampleDrafts.Key("feature:tabs"));
        var proposal = new MapOverride(OverrideAction.Propose, SampleDrafts.Key("feature:tabs.pinning"), "service:engine", SampleDrafts.At, "Tab pinning", Proposal: run);

        var queue = Queue(proposal, SampleDrafts.Row(OverrideAction.Accept, "feature:tabs.pinning", at: SampleDrafts.At.AddMinutes(1)));

        Assert.Equal(1, queue.Decided);
        Assert.DoesNotContain(queue.Entries, e => e.Node.Value == "feature:tabs.pinning");
    }

    [Fact, Trait("Lane", "unit")]
    public void Queue_caps_each_group_and_keeps_full_counts()
    {
        var builder = new GraphBuilder().Node("feature:tabs");
        for (var i = 0; i < 3; i++)
        {
            _ = builder.Node("surface:command/c" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), provenance: Provenance.Deterministic);
        }

        _ = builder.Node("feature:m", provenance: Provenance.Model);
        var full = ReviewBacklog.Build("v1@a", builder.Build(), []);

        var capped = full.Capped(2);

        Assert.Equal(["deterministic", "deterministic", "model"], capped.Entries.Select(e => e.Group.ToString().ToLowerInvariant()), StringComparer.Ordinal);
        Assert.Equal(new QueueCounts(3, 1, 0), capped.Counts);
        Assert.Equal(new ReviewTruncated(Deterministic: true, Model: false, Orphaned: false), capped.Truncated);
        Assert.Equal(4, capped.Undecided);
        Assert.Equal(full.Truncated, ReviewTruncated.None);
    }
}
