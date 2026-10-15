using Aw.Context.Core;
using Aw.Context.Core.Overrides;
using Aw.Context.Tests.Review;

namespace Aw.Context.Tests.Overrides;

public sealed class UndoRulesTests
{
    private static readonly DateTimeOffset _t2 = SampleDrafts.At.AddMinutes(1);

    [Fact, Trait("Lane", "unit")]
    public void A_known_override_can_be_undone()
    {
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:a");

        var found = UndoRules.Check([reject], reject.ToId());

        Assert.Same(reject, found.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_id_is_a_stale_review_target()
    {
        var found = UndoRules.Check([SampleDrafts.Row(OverrideAction.Reject, "feature:a")], OverrideId.From("ovr_0000000000000000"));

        Assert.Equal(ContextErrors.ReviewTargetStale.Code, found.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_override_that_is_already_undone_is_a_stale_review_target()
    {
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:a");

        var found = UndoRules.Check([reject, SampleDrafts.Undo(reject, _t2)], reject.ToId());

        Assert.Equal(ContextErrors.ReviewTargetStale.Code, found.Error.Code);
        Assert.Contains("already undone", found.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_undo_can_itself_be_undone_once()
    {
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:a");
        var undo = SampleDrafts.Undo(reject, _t2);

        Assert.True(UndoRules.Check([reject, undo], undo.ToId()).IsSuccess);
        Assert.True(UndoRules.Check([reject, undo, SampleDrafts.Undo(undo, _t2.AddMinutes(1))], undo.ToId()).IsError);
    }
}
