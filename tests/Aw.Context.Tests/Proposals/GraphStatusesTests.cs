using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Tests.Review;

namespace Aw.Context.Tests.Proposals;

public sealed class GraphStatusesTests
{
    private static MapOverride Row(string run, string key, string name, NodeKind kind = NodeKind.Feature) =>
        new(OverrideAction.Propose, SampleDrafts.Key(key), "service:engine", SampleDrafts.At, name,
            Proposal: new ProposalPayload(run, ContextRepoName.From("aw-split-panes"), kind, SampleDrafts.Key("feature:tabs")));

    [Fact, Trait("Lane", "unit")]
    public void A_run_gets_back_its_active_proposals_in_order()
    {
        var a = Row("run-1", "feature:a", "A");
        var b = Row("run-2", "feature:b", "B");
        var c = Row("run-1", "feature:c", "C") with { At = SampleDrafts.At.AddMinutes(1) };

        var surfaces = GraphStatuses.ForRun("run-1", [c, b, a]);

        Assert.Equal(["A", "C"], surfaces.Select(s => s.Name), StringComparer.Ordinal);
        Assert.Equal(["feature:a", "feature:c"], surfaces.Select(s => s.Key?.Value), StringComparer.Ordinal);
        Assert.All(surfaces, s => Assert.Equal((NodeKind.Feature, "feature:tabs"), (s.Kind, s.Parent?.Value)));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_undone_proposal_is_no_longer_the_runs()
    {
        var a = Row("run-1", "feature:a", "A");

        Assert.Empty(GraphStatuses.ForRun("run-1", [a, SampleDrafts.Undo(a, SampleDrafts.At.AddMinutes(1))]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_surface_is_mapped_new_or_unmapped_against_current_and_the_draft()
    {
        var current = SampleDrafts.Draft();
        var draft = OverrideApplier.Apply(current, [Row("run-1", "feature:pinning", "Pinning")]).Graph;
        ProposedSurface[] surfaces =
        [
            Surfaces.Of("Layout", "feature:layout", NodeKind.Feature),
            Surfaces.Of("Pinning", "feature:pinning", NodeKind.Feature),
            Surfaces.Of("Mystery", "feature:mystery", NodeKind.Feature),
            Surfaces.Of("No kind"),
        ];

        var statuses = GraphStatuses.Of(surfaces, current, draft);

        Assert.Equal([GraphStatusKind.Mapped, GraphStatusKind.New, GraphStatusKind.Unmapped, GraphStatusKind.Unmapped], statuses.Select(s => s.Status));
        Assert.Equal("feature:pinning", statuses[1].Node?.Value);
        Assert.Null(statuses[2].Node);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_proposal_turns_from_new_to_mapped_when_the_draft_becomes_current()
    {
        var current = SampleDrafts.Draft();
        var draft = OverrideApplier.Apply(current, [Row("run-1", "feature:pinning", "Pinning")]).Graph;
        ProposedSurface[] surfaces = [Surfaces.Of("Pinning", "feature:pinning", NodeKind.Feature)];

        Assert.Equal(GraphStatusKind.New, GraphStatuses.Of(surfaces, current, draft).Single().Status);
        Assert.Equal(GraphStatusKind.Mapped, GraphStatuses.Of(surfaces, draft, draft).Single().Status);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_no_draft_a_missing_surface_is_unmapped()
    {
        ProposedSurface[] surfaces = [Surfaces.Of("Pinning", "feature:pinning", NodeKind.Feature)];

        Assert.Equal(GraphStatusKind.Unmapped, GraphStatuses.Of(surfaces, SampleDrafts.Draft(), draft: null).Single().Status);
    }
}
