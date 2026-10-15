using Aw.Context.Core.Overrides;
using Aw.Context.Tests.Review;

namespace Aw.Context.Tests.Overrides;

public sealed class OverrideLogTests
{
    private static readonly DateTimeOffset _t1 = SampleDrafts.At;
    private static readonly DateTimeOffset _t2 = SampleDrafts.At.AddMinutes(1);
    private static readonly DateTimeOffset _t3 = SampleDrafts.At.AddMinutes(2);

    [Fact, Trait("Lane", "unit")]
    public void Ordered_sorts_by_time_then_by_id()
    {
        var late = SampleDrafts.Row(OverrideAction.Rename, "feature:a", "A", at: _t2);
        var early = SampleDrafts.Row(OverrideAction.Accept, "feature:b", at: _t1);
        var tie1 = SampleDrafts.Row(OverrideAction.Accept, "feature:c", at: _t2);

        var ordered = OverrideLog.Ordered([late, tie1, early]);

        Assert.Same(early, ordered[0]);
        var tied = new[] { late, tie1 }.OrderBy(o => o.ToId().Value, StringComparer.Ordinal).ToList();
        Assert.Same(tied[0], ordered[1]);
        Assert.Same(tied[1], ordered[2]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_undo_reverses_the_override_it_names()
    {
        var rename = SampleDrafts.Row(OverrideAction.Rename, "feature:a", "A", at: _t1);
        var undo = SampleDrafts.Undo(rename, _t2);

        var undone = OverrideLog.Undone([rename, undo]);

        Assert.Equal([rename.ToId().Value], undone, StringComparer.Ordinal);
        Assert.Empty(OverrideLog.Active([rename, undo]));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_undo_that_is_itself_undone_reverses_nothing_so_the_first_row_counts_again()
    {
        var rename = SampleDrafts.Row(OverrideAction.Rename, "feature:a", "A", at: _t1);
        var undo = SampleDrafts.Undo(rename, _t2);
        var redo = SampleDrafts.Undo(undo, _t3);

        var active = OverrideLog.Active([rename, undo, redo]);

        Assert.Same(rename, Assert.Single(active));
        Assert.Equal([undo.ToId().Value], OverrideLog.Undone([rename, undo, redo]), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_undo_naming_an_unknown_override_changes_nothing()
    {
        var accept = SampleDrafts.Row(OverrideAction.Accept, "feature:a", at: _t1);
        var stray = SampleDrafts.Undo(SampleDrafts.Row(OverrideAction.Reject, "feature:zzz", at: _t1), _t2);

        Assert.Same(accept, Assert.Single(OverrideLog.Active([accept, stray])));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(OverrideAction.Accept, true)]
    [InlineData(OverrideAction.Reject, true)]
    [InlineData(OverrideAction.Merge, true)]
    [InlineData(OverrideAction.Delete, true)]
    [InlineData(OverrideAction.Rename, false)]
    [InlineData(OverrideAction.Move, false)]
    [InlineData(OverrideAction.Add, false)]
    [InlineData(OverrideAction.Propose, false)]
    [InlineData(OverrideAction.Undo, false)]
    public void Decides_names_the_actions_that_settle_a_proposal(OverrideAction action, bool expected) =>
        Assert.Equal(expected, OverrideLog.Decides(SampleDrafts.Row(action, "feature:a", "A", "feature:b")));
}
