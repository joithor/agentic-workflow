using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ProposeFromScope;

/// <summary>An empty surface list takes a scoping run back (scoping spec C9): its undecided proposals get an undo row each, accepted ones stay, and no draft is opened.</summary>
public sealed class WithdrawTests : IDisposable
{
    private static readonly ContextRepoName _repo = ContextRepoName.From(FixtureMap.Repo);
    private static readonly NodeKey _restore = NodeKey.From("feature:tabs.restore-session");
    private static readonly NodeKey _zoom = NodeKey.From("feature:tabs.zoom");
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ScopeProposal Run(string run = "run-1") => new(run, _repo,
    [
        new ProposedSurface("Restore session", _restore, NodeKind.Feature, Parent: null, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/src/tabs/restore.ts"))], "reopen the tabs I had"),
        new ProposedSurface("Zoom", _zoom, NodeKind.Feature, Parent: null, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/src/tabs/zoom.ts"))], Quote: null),
    ]);

    private static ScopeProposal Empty(string run = "run-1") => new(run, _repo, []);

    private async Task<ProposeResult> Propose(ScopeProposal proposal) => (await _world.Map.Propose(proposal, MapWorld.Human, MapWorld.None)).Value;

    private async Task<List<MapOverride>> Overrides() => [.. (await _world.Store.Overrides(MapWorld.None)).Value];

    [Fact, Trait("Lane", "unit")]
    public async Task Empty_surfaces_withdraws_the_runs_undecided_proposals_with_undo_rows()
    {
        _ = await _world.ImportFixture();
        _ = await Propose(Run());

        var withdrawn = await Propose(Empty());

        Assert.Equal((2, 0), (withdrawn.Withdrawn, withdrawn.Kept));
        var rows = await Overrides();
        Assert.Equal(2, rows.Count(o => o.Action == OverrideAction.Undo));
        Assert.DoesNotContain(OverrideLog.Active(rows), o => o.Action == OverrideAction.Propose);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Withdraw_keeps_accepted_proposals()
    {
        _ = await _world.ImportFixture();
        _ = await Propose(Run());
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.Accept, _restore), MapWorld.Human, MapWorld.None)).IsSuccess);

        var withdrawn = await Propose(Empty());

        Assert.Equal((1, 1), (withdrawn.Withdrawn, withdrawn.Kept));
        var active = OverrideLog.Active(await Overrides());
        Assert.Equal([_restore.Value], active.Where(o => o.Action == OverrideAction.Propose).Select(o => o.Target.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Withdraw_keeps_a_proposal_a_person_renamed()
    {
        _ = await _world.ImportFixture();
        _ = await Propose(Run());
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.Rename, _restore, Label: "Restore tabs"), MapWorld.Human, MapWorld.None)).IsSuccess);

        var withdrawn = await Propose(Empty());

        Assert.Equal((1, 1), (withdrawn.Withdrawn, withdrawn.Kept));   // the zoom proposal is undone; the renamed one is a person's decision and stays
        var undone = (await Overrides()).Where(o => o.Action == OverrideAction.Undo).Select(o => o.Target.Value);
        Assert.Equal([_zoom.Value], undone, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Withdraw_keeps_a_proposal_a_person_moved()
    {
        _ = await _world.ImportFixture();
        _ = await Propose(Run());
        _world.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.Move, _restore, Other: NodeKey.From("feature:tabs.tab-manager")), MapWorld.Human, MapWorld.None)).IsSuccess);

        var withdrawn = await Propose(Empty());

        Assert.Equal((1, 1), (withdrawn.Withdrawn, withdrawn.Kept));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repeated_withdraw_names_the_runs_earlier_undo_rows_so_a_failed_event_can_be_written_by_the_retry()
    {
        _ = await _world.ImportFixture();
        _ = await Propose(Run());
        var first = await Propose(Empty());

        var again = await Propose(Empty());

        Assert.Equal(0, again.Withdrawn);   // nothing new was taken back
        Assert.Equal(2, first.WithdrawnRows!.Count);
        Assert.Equal(first.WithdrawnRows.Select(i => i.Value), again.WithdrawnRows!.Select(i => i.Value), StringComparer.Ordinal);   // but the rows the first call wrote are named again
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Withdraw_with_no_draft_opens_none_and_writes_nothing()
    {
        _ = await _world.ImportFixture();
        var before = await Overrides();

        var withdrawn = await Propose(Empty());

        Assert.Null(withdrawn.Draft);
        Assert.Equal((0, 0), (withdrawn.Withdrawn, withdrawn.Kept));
        Assert.DoesNotContain((await _world.Store.Versions(MapWorld.None)).Value, v => v.Status == Context.Core.Map.MapVersionStatus.Draft);
        Assert.Equal(before.Count, (await Overrides()).Count);   // why: the imported fixture carries overrides of its own; the withdraw adds none
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Withdraw_twice_writes_nothing_the_second_time()
    {
        _ = await _world.ImportFixture();
        _ = await Propose(Run());
        _ = await Propose(Empty());
        var before = (await Overrides()).Count;
        _world.Clock.Advance(TimeSpan.FromMinutes(1));

        var again = await Propose(Empty());

        Assert.Equal((0, 0), (again.Withdrawn, again.Kept));
        Assert.Equal(before, (await Overrides()).Count);
        Assert.Equal(2, again.WithdrawnRows!.Count);   // it names the earlier undo rows again (and writes none) so a failed event append can be retried
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Withdraw_touches_only_that_run()
    {
        _ = await _world.ImportFixture();
        _ = await Propose(Run("run-1"));
        _ = await Propose(new ScopeProposal("run-2", _repo, [new ProposedSurface("Pin", NodeKey.From("feature:tabs.pin"), NodeKind.Feature, Parent: null, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/src/tabs/pin.ts"))], Quote: null)]));

        var withdrawn = await Propose(Empty("run-2"));

        Assert.Equal(0, withdrawn.Kept);
        var active = OverrideLog.Active(await Overrides()).Where(o => o.Action == OverrideAction.Propose).ToList();
        Assert.Equal(["run-1", "run-1"], active.Select(o => o.Proposal!.Run), StringComparer.Ordinal);
        Assert.Equal(1, withdrawn.Withdrawn);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Withdrawn_proposals_leave_the_review_queue_and_promote_unblocks()
    {
        _ = await _world.ImportFixture();
        _ = await Propose(Run());
        var blocked = await _world.Map.Promote(MapWorld.Human, MapWorld.None);
        _ = await Propose(Empty());

        var queue = (await _world.Map.ReviewQueue(limit: null, MapWorld.Human, MapWorld.None)).Value;
        var promoted = await _world.Map.Promote(MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.PromoteRefused.Code, blocked.Error.Code);
        Assert.DoesNotContain(queue.Entries, e => e.Node.Value == _restore.Value || e.Node.Value == _zoom.Value);
        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Proposals_view_after_withdraw_lists_no_surfaces()
    {
        _ = await _world.ImportFixture();
        _ = await Propose(Run());

        _ = await Propose(Empty());

        var view = (await _world.Map.Proposals("run-1", MapWorld.Human, MapWorld.None)).Value;
        Assert.Empty(view.Surfaces);
    }
}
