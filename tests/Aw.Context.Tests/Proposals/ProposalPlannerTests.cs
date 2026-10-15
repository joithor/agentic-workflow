using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Tests.Review;

namespace Aw.Context.Tests.Proposals;

public sealed class ProposalPlannerTests
{
    private static readonly EvidenceRef[] _evidence = [new(RecordKey.From("linear:issue/ENG-1"), "span-a")];

    private static ProposedSurface Pinning(EvidenceRef[]? evidence = null) =>
        new("Tab pinning", Key: null, NodeKind.Feature, SampleDrafts.Key("feature:tabs"), evidence ?? _evidence, "pin a tab");

    private static ScopeProposal Run(params ProposedSurface[] surfaces) => new("run-1", ContextRepoName.From("aw-split-panes"), surfaces);

    private static ProposalPlan Plan(ScopeProposal proposal, params MapOverride[] overrides) =>
        ProposalPlanner.Plan(proposal, SampleDrafts.Draft(), overrides, "service:engine", SampleDrafts.At);

    [Fact, Trait("Lane", "unit")]
    public void A_new_surface_becomes_one_propose_row_with_the_runs_payload()
    {
        var plan = Plan(Run(Pinning()));

        var row = Assert.Single(plan.Rows);
        Assert.Equal((OverrideAction.Propose, "feature:tab pinning", "service:engine", "Tab pinning"), (row.Action, row.Target.Value, row.Who, row.Label));
        var payload = row.Proposal!;   // why: a propose row carries its payload
        Assert.Equal(("run-1", "aw-split-panes", NodeKind.Feature, "feature:tabs", "pin a tab"), (payload.Run, payload.Repo.Value, payload.Kind, payload.Parent?.Value, payload.Quote));
        Assert.Equal(_evidence, payload.Evidence);
        Assert.Equal((0, 0, 0), (plan.Existing, plan.Rejected, plan.Unmapped));
    }

    [Fact, Trait("Lane", "unit")]
    public void Proposing_twice_is_idempotent_over_the_rows_already_written()
    {
        var first = Plan(Run(Pinning()));

        var again = Plan(Run(Pinning()), [.. first.Rows]);

        Assert.Empty(again.Rows);
        Assert.Equal(1, again.Existing);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_surface_repeated_in_one_request_is_one_row()
    {
        var plan = Plan(Run(Pinning(), Pinning()));

        _ = Assert.Single(plan.Rows);
        Assert.Equal(1, plan.Existing);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_surface_the_map_already_has_is_existing_and_writes_nothing()
    {
        var plan = Plan(Run(Surfaces.Of("Layout", "feature:layout", NodeKind.Feature)));

        Assert.Empty(plan.Rows);
        Assert.Equal(1, plan.Existing);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_surface_a_person_rejected_is_not_proposed_again()
    {
        var plan = Plan(Run(Pinning()));
        var proposed = OverrideApplier.Apply(SampleDrafts.Draft(), plan.Rows).Graph;
        var queue = ReviewBacklog.Build("v1@a", SampleDrafts.Draft(), plan.Rows);
        Assert.Contains(queue.Entries, e => e.Node.Value == "feature:tab pinning");
        var rejected = ReviewPlanner.Plan(new ReviewDecision(ReviewVerb.Reject, SampleDrafts.Key("feature:tab pinning")), SampleDrafts.Draft(), plan.Rows, "avery", SampleDrafts.At.AddMinutes(1)).Value;

        var again = Plan(Run(Pinning()), [.. plan.Rows, .. rejected]);

        Assert.NotNull(proposed.Resolve(SampleDrafts.Key("feature:tab pinning")));
        Assert.Empty(again.Rows);
        Assert.Equal(1, again.Rejected);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reject_that_was_undone_no_longer_blocks_the_surface_and_it_is_in_the_queue_again()
    {
        var plan = Plan(Run(Pinning()));
        var rejected = ReviewPlanner.Plan(new ReviewDecision(ReviewVerb.Reject, SampleDrafts.Key("feature:tab pinning")), SampleDrafts.Draft(), plan.Rows, "avery", SampleDrafts.At.AddMinutes(1)).Value;
        var undone = ReviewPlanner.Plan(new ReviewDecision(ReviewVerb.Undo, Undoes: rejected[0].ToId()), SampleDrafts.Draft(), [.. plan.Rows, .. rejected], "avery", SampleDrafts.At.AddMinutes(2)).Value;
        MapOverride[] history = [.. plan.Rows, .. rejected, .. undone];

        var again = Plan(Run(Pinning()), history);

        Assert.Equal(0, again.Rejected);
        Assert.Equal(1, again.Existing);   // the proposal row is still there: nothing is written twice
        Assert.Contains(ReviewBacklog.Build("v1@a", SampleDrafts.Draft(), history).Entries, e => e.Node.Value == "feature:tab pinning");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rejected_surface_comes_back_when_its_evidence_changed()
    {
        var plan = Plan(Run(Pinning()));
        var rejected = ReviewPlanner.Plan(new ReviewDecision(ReviewVerb.Reject, SampleDrafts.Key("feature:tab pinning")), SampleDrafts.Draft(), plan.Rows, "avery", SampleDrafts.At.AddMinutes(1)).Value;
        EvidenceRef[] edited = [new(RecordKey.From("linear:issue/ENG-1"), "span-b")];

        var again = Plan(Run(Pinning(edited)), [.. plan.Rows, .. rejected]);

        _ = Assert.Single(again.Rows);
        Assert.Equal(0, again.Rejected);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_surface_with_no_kind_and_no_key_is_unmapped_not_proposed()
    {
        var plan = Plan(Run(Surfaces.Of("Something"), Pinning()));

        _ = Assert.Single(plan.Rows);
        Assert.Equal(1, plan.Unmapped);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_surface_whose_propose_row_a_person_undid_is_proposed_again_as_a_new_row_the_undo_stands()
    {
        var first = Assert.Single(Plan(Run(Pinning())).Rows);
        var undo = new MapOverride(OverrideAction.Undo, first.Target, "thor", SampleDrafts.At.AddMinutes(1), Undoes: first.ToId());

        var again = Plan(Run(Pinning()), first, undo);
        var next = Assert.Single(again.Rows);
        var stable = Plan(Run(Pinning()), first, undo, next);

        Assert.NotEqual(first.ToId().Value, next.ToId().Value, StringComparer.Ordinal);
        Assert.Empty(stable.Rows);   // the new row stands, so a third submission finds the surface there
        Assert.Equal(1, stable.Existing);
    }
}
