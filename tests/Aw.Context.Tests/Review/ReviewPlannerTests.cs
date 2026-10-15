using Aw.Context.Core;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Review;
using Aw.Kernel;

namespace Aw.Context.Tests.Review;

public sealed class ReviewPlannerTests
{
    private const string Who = "avery";

    private static Result<IReadOnlyList<MapOverride>> Plan(ReviewDecision decision, params MapOverride[] overrides) =>
        ReviewPlanner.Plan(decision, SampleDrafts.Draft(), overrides, Who, SampleDrafts.At);

    private static ReviewDecision On(ReviewVerb verb, string node, string? label = null, string? other = null) =>
        new(verb, SampleDrafts.Key(node), Label: label, Other: other is null ? null : SampleDrafts.Key(other));

    [Fact, Trait("Lane", "unit")]
    public void Accepting_a_queued_node_is_one_accept_row()
    {
        var rows = Plan(On(ReviewVerb.Accept, "feature:layout")).Value;

        var row = Assert.Single(rows);
        Assert.Equal((OverrideAction.Accept, "feature:layout", Who, SampleDrafts.At), (row.Action, row.Target.Value, row.Who, row.At));
        Assert.Null(row.Link);
    }

    [Fact, Trait("Lane", "unit")]
    public void Rejecting_a_node_records_its_rejection_identity_so_it_is_not_re_proposed()
    {
        var row = Assert.Single(Plan(On(ReviewVerb.Reject, "feature:layout")).Value);

        var node = SampleDrafts.Draft().Resolve(SampleDrafts.Key("feature:layout"))!;   // why: the draft holds the node
        Assert.Equal(OverrideAction.Reject, row.Action);
        Assert.Equal(RejectionIdentity.For(node), row.Rejection);
    }

    [Fact, Trait("Lane", "unit")]
    public void Accepting_or_rejecting_a_link_is_one_row_that_names_the_link_and_its_from_node()
    {
        var link = SampleDrafts.Link("code:src/layout/Panes.ts", EdgeType.Implements, "feature:layout");

        var accept = Assert.Single(Plan(new ReviewDecision(ReviewVerb.Accept, Link: link)).Value);
        var reject = Assert.Single(Plan(new ReviewDecision(ReviewVerb.Reject, Link: link)).Value);

        Assert.Equal((OverrideAction.Accept, link, "code:src/layout/Panes.ts"), (accept.Action, accept.Link, accept.Target.Value));
        Assert.Equal((OverrideAction.Reject, link), (reject.Action, reject.Link));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_bulk_accept_writes_one_row_per_deterministic_node_and_nothing_for_model_ones()
    {
        var rows = Plan(new ReviewDecision(ReviewVerb.AcceptDeterministic)).Value;

        Assert.Equal(["surface:command/close-tab", "surface:command/switch-tab"], rows.Select(r => r.Target.Value), StringComparer.Ordinal);
        Assert.All(rows, r => Assert.Equal(OverrideAction.Accept, r.Action));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_bulk_accept_skips_what_is_decided_already()
    {
        var done = SampleDrafts.Row(OverrideAction.Accept, "surface:command/close-tab");

        var rows = Plan(new ReviewDecision(ReviewVerb.AcceptDeterministic), done).Value;

        Assert.Equal(["surface:command/switch-tab"], rows.Select(r => r.Target.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_target_decided_already_is_stale_047()
    {
        var plan = Plan(On(ReviewVerb.Accept, "feature:layout"), SampleDrafts.Row(OverrideAction.Reject, "feature:layout"));

        Assert.Equal(ContextErrors.ReviewTargetStale.Code, plan.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("feature:nowhere")]
    [InlineData("feature:tabs")]
    public void A_node_that_is_not_in_the_queue_is_stale_047(string node) =>
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(On(ReviewVerb.Accept, node)).Error.Code);

    [Fact, Trait("Lane", "unit")]
    public void A_link_that_is_not_in_the_queue_is_stale_047()
    {
        var unknown = SampleDrafts.Link("code:src/nope.ts", EdgeType.Implements, "feature:layout");

        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(new ReviewDecision(ReviewVerb.Accept, Link: unknown)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Undo_writes_an_undo_row_that_names_the_override()
    {
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:layout");

        var row = Assert.Single(Plan(new ReviewDecision(ReviewVerb.Undo, Undoes: reject.ToId()), reject).Value);

        Assert.Equal((OverrideAction.Undo, reject.ToId()), (row.Action, row.Undoes));
    }

    [Fact, Trait("Lane", "unit")]
    public void Undo_of_an_unknown_or_already_undone_override_is_stale_047()
    {
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:layout");

        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(new ReviewDecision(ReviewVerb.Undo, Undoes: reject.ToId())).Error.Code);
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(new ReviewDecision(ReviewVerb.Undo, Undoes: reject.ToId()), reject, SampleDrafts.Undo(reject, SampleDrafts.At.AddMinutes(1))).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Dropping_an_orphan_undoes_it_and_dropping_anything_else_is_stale_047()
    {
        var orphan = SampleDrafts.Row(OverrideAction.Rename, "feature:gone", "Gone");
        var live = SampleDrafts.Row(OverrideAction.Rename, "feature:layout", "Panes");

        var row = Assert.Single(Plan(new ReviewDecision(ReviewVerb.Drop, Undoes: orphan.ToId()), orphan, live).Value);

        Assert.Equal((OverrideAction.Undo, orphan.ToId()), (row.Action, row.Undoes));
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(new ReviewDecision(ReviewVerb.Drop, Undoes: live.ToId()), orphan, live).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Repoint_writes_an_undo_of_the_orphan_and_the_same_decision_on_the_new_node()
    {
        var orphan = SampleDrafts.Row(OverrideAction.Rename, "feature:gone", "Panes");
        var decision = new ReviewDecision(ReviewVerb.Repoint, SampleDrafts.Key("feature:layout"), Undoes: orphan.ToId());

        var rows = Plan(decision, orphan).Value;

        Assert.Equal([OverrideAction.Undo, OverrideAction.Rename], rows.Select(r => r.Action));
        Assert.Equal(orphan.ToId(), rows[0].Undoes);
        Assert.Equal(("feature:layout", "Panes", Who, SampleDrafts.At), (rows[1].Target.Value, rows[1].Label, rows[1].Who, rows[1].At));
    }

    [Fact, Trait("Lane", "unit")]
    public void Repoint_to_a_node_not_in_the_draft_is_047()
    {
        var orphan = SampleDrafts.Row(OverrideAction.Rename, "feature:gone", "Panes");
        var live = SampleDrafts.Row(OverrideAction.Rename, "feature:layout", "Panes");

        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(new ReviewDecision(ReviewVerb.Repoint, SampleDrafts.Key("feature:nowhere"), Undoes: orphan.ToId()), orphan).Error.Code);
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(new ReviewDecision(ReviewVerb.Repoint, SampleDrafts.Key("feature:tabs"), Undoes: live.ToId()), orphan, live).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Undoing_both_rows_of_a_repoint_restores_the_orphan()
    {
        var orphan = SampleDrafts.Row(OverrideAction.Rename, "feature:gone", "Panes");
        var rows = ReviewPlanner.Plan(new ReviewDecision(ReviewVerb.Repoint, SampleDrafts.Key("feature:layout"), Undoes: orphan.ToId()), SampleDrafts.Draft(), [orphan], Who, SampleDrafts.At.AddMinutes(1)).Value;
        var log = new[] { orphan }.Concat(rows).ToArray();

        var undo = ReviewPlanner.Plan(new ReviewDecision(ReviewVerb.Undo, UndoesAll: [rows[0].ToId(), rows[1].ToId()]), SampleDrafts.Draft(), log, Who, SampleDrafts.At.AddMinutes(2)).Value;

        Assert.Equal([OverrideAction.Undo, OverrideAction.Undo], undo.Select(r => r.Action));
        Assert.Equal([rows[0].ToId(), rows[1].ToId()], undo.Select(r => r.Undoes!.Value));
        var applied = OverrideApplier.Apply(SampleDrafts.Draft(), [.. log, .. undo]);
        Assert.Equal(orphan.ToId(), Assert.Single(applied.Orphaned).ToId());
        Assert.DoesNotContain(OverrideLog.Active([.. log, .. undo]), o => o.Target.Value == "feature:layout");
    }

    [Fact, Trait("Lane", "unit")]
    public void Undo_of_a_list_is_all_or_none()
    {
        var accept = SampleDrafts.Row(OverrideAction.Accept, "surface:command/close-tab");
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:layout", at: SampleDrafts.At.AddMinutes(1));

        var rows = Plan(new ReviewDecision(ReviewVerb.Undo, UndoesAll: [accept.ToId(), reject.ToId()]), accept, reject).Value;

        Assert.Equal([accept.ToId(), reject.ToId()], rows.Select(r => r.Undoes!.Value));
        Assert.All(rows, r => Assert.Equal((OverrideAction.Undo, Who, SampleDrafts.At), (r.Action, r.Who, r.At)));
        Assert.Equal(["surface:command/close-tab", "feature:layout"], rows.Select(r => r.Target.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Undo_list_with_one_undone_id_is_047_and_writes_nothing()
    {
        var accept = SampleDrafts.Row(OverrideAction.Accept, "surface:command/close-tab");
        var reject = SampleDrafts.Row(OverrideAction.Reject, "feature:layout", at: SampleDrafts.At.AddMinutes(1));
        var undoReject = SampleDrafts.Undo(reject, SampleDrafts.At.AddMinutes(2));

        var plan = Plan(new ReviewDecision(ReviewVerb.Undo, UndoesAll: [accept.ToId(), reject.ToId()]), accept, reject, undoReject);

        Assert.Equal(ContextErrors.ReviewTargetStale.Code, plan.Error.Code);
        Assert.Contains(reject.ToId().Value, plan.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(accept.ToId().Value, plan.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Undo_list_names_the_first_stale_id_and_an_unknown_id_is_stale_too()
    {
        var accept = SampleDrafts.Row(OverrideAction.Accept, "surface:command/close-tab");
        var unknown = OverrideId.From("ovr_0123456789abcdef");

        var plan = Plan(new ReviewDecision(ReviewVerb.Undo, UndoesAll: [unknown, accept.ToId()]), accept);

        Assert.Equal(ContextErrors.ReviewTargetStale.Code, plan.Error.Code);
        Assert.Contains(unknown.Value, plan.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Undo_list_ignores_a_repeated_id_and_an_empty_list_names_no_override()
    {
        var accept = SampleDrafts.Row(OverrideAction.Accept, "surface:command/close-tab");

        var rows = Plan(new ReviewDecision(ReviewVerb.Undo, UndoesAll: [accept.ToId(), accept.ToId()]), accept).Value;
        var empty = Plan(new ReviewDecision(ReviewVerb.Undo, UndoesAll: []), accept);

        _ = Assert.Single(rows);
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, empty.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Rename_move_merge_and_delete_work_on_any_node_of_the_draft()
    {
        Assert.Equal((OverrideAction.Rename, "Tab strip"), Row(On(ReviewVerb.Rename, "feature:tabs", label: "Tab strip"), r => (r.Action, r.Label!)));
        Assert.Equal((OverrideAction.Move, "feature:layout"), Row(On(ReviewVerb.Move, "feature:tabs.groups", other: "feature:layout"), r => (r.Action, r.Other!.Value.Value)));
        Assert.Equal((OverrideAction.Merge, "feature:tabs"), Row(On(ReviewVerb.Merge, "feature:layout", other: "feature:tabs"), r => (r.Action, r.Other!.Value.Value)));
        Assert.Equal(OverrideAction.Delete, Row(On(ReviewVerb.Delete, "feature:layout"), r => r.Action));
    }

    [Fact, Trait("Lane", "unit")]
    public void Rename_needs_a_label_and_a_move_needs_a_feature_to_go_under()
    {
        Assert.Equal(ContextErrors.QueryInvalid.Code, Plan(On(ReviewVerb.Rename, "feature:layout", label: " ")).Error.Code);
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(On(ReviewVerb.Move, "feature:layout", other: "feature:missing")).Error.Code);
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(On(ReviewVerb.Move, "feature:layout", other: "feature:layout")).Error.Code);
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(On(ReviewVerb.Merge, "feature:layout", other: "surface:command/close-tab")).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Add_creates_a_node_the_draft_lacks_and_refuses_one_it_has()
    {
        var added = Assert.Single(Plan(On(ReviewVerb.Add, "feature:tabs.pinning", label: "Pinning", other: "feature:tabs")).Value);

        Assert.Equal((OverrideAction.Add, "Pinning", "feature:tabs"), (added.Action, added.Label, added.Other?.Value));
        Assert.Equal(ContextErrors.ReviewTargetStale.Code, Plan(On(ReviewVerb.Add, "feature:layout", label: "Layout")).Error.Code);
        Assert.Equal(ContextErrors.QueryInvalid.Code, Plan(On(ReviewVerb.Add, "feature:tabs.pinning")).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_decision_that_names_no_node_is_006() =>
        Assert.Equal(ContextErrors.QueryInvalid.Code, Plan(new ReviewDecision(ReviewVerb.Rename, Label: "x")).Error.Code);

    private static T Row<T>(ReviewDecision decision, Func<MapOverride, T> read) => read(Assert.Single(Plan(decision).Value));
}
