using Aw.Context.Core.Overrides;
using Aw.Context.Tests.Review;

namespace Aw.Context.Tests.Overrides;

public sealed class UndoOverrideTests
{
    private static readonly DateTimeOffset _t2 = SampleDrafts.At.AddMinutes(1);
    private static readonly DateTimeOffset _t3 = SampleDrafts.At.AddMinutes(2);

    [Fact, Trait("Lane", "unit")]
    public void Undoing_a_rename_restores_the_label()
    {
        var rename = SampleDrafts.Row(OverrideAction.Rename, "feature:layout", "Panes");

        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [rename, SampleDrafts.Undo(rename, _t2)]);

        Assert.Equal("Layout", applied.Graph.Resolve(SampleDrafts.Key("feature:layout"))?.Label);
    }

    [Fact, Trait("Lane", "unit")]
    public void Undoing_a_reject_brings_the_proposal_back()
    {
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:layout");

        var rejected = OverrideApplier.Apply(SampleDrafts.Draft(), [reject]);
        var undone = OverrideApplier.Apply(SampleDrafts.Draft(), [reject, SampleDrafts.Undo(reject, _t2)]);

        Assert.Null(rejected.Graph.Resolve(SampleDrafts.Key("feature:layout")));
        Assert.NotNull(undone.Graph.Resolve(SampleDrafts.Key("feature:layout")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Undoing_an_undo_applies_the_first_override_again()
    {
        var rename = SampleDrafts.Row(OverrideAction.Rename, "feature:layout", "Panes");
        var undo = SampleDrafts.Undo(rename, _t2);

        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [rename, undo, SampleDrafts.Undo(undo, _t3)]);

        Assert.Equal("Panes", applied.Graph.Resolve(SampleDrafts.Key("feature:layout"))?.Label);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_orphaned_override_that_is_undone_leaves_the_orphan_list()
    {
        var orphan = SampleDrafts.Row(OverrideAction.Rename, "feature:gone", "Gone");

        Assert.Same(orphan, Assert.Single(OverrideApplier.Apply(SampleDrafts.Draft(), [orphan]).Orphaned));
        Assert.Empty(OverrideApplier.Apply(SampleDrafts.Draft(), [orphan, SampleDrafts.Undo(orphan, _t2)]).Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_order_the_rows_arrive_in_does_not_change_the_result()
    {
        var rename = SampleDrafts.Row(OverrideAction.Rename, "feature:layout", "Panes");
        var undo = SampleDrafts.Undo(rename, _t2);
        var accept = SampleDrafts.Row(OverrideAction.Accept, "feature:tabs.groups", at: _t3);

        var forward = OverrideApplier.Apply(SampleDrafts.Draft(), [rename, undo, accept]).Graph;
        var backward = OverrideApplier.Apply(SampleDrafts.Draft(), [accept, undo, rename]).Graph;

        Assert.Equal(forward.Nodes, backward.Nodes);
        Assert.Equal(forward.Edges, backward.Edges);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_row_with_no_s4_fields_keeps_the_id_it_always_had()
    {
        var plain = SampleDrafts.Row(OverrideAction.Rename, "feature:layout", "Panes");

        Assert.Matches("^ovr_[0-9a-f]{16}$", plain.ToId().Value);
        Assert.NotEqual(plain.ToId(), (plain with { Undoes = plain.ToId() }).ToId());
        Assert.NotEqual(plain.ToId(), (plain with { Undoes = plain.ToId() }).ToId());
    }
}
