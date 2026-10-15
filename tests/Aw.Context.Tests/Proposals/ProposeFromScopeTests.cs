using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.Proposals;

public sealed class ProposeFromScopeTests : IDisposable
{
    private static readonly ContextRepoName _repo = ContextRepoName.From(FixtureMap.Repo);
    private static readonly NodeKey _restore = NodeKey.From("feature:tabs.restore-session");
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ScopeProposal Run(string run = "run-1") => new(run, _repo,
    [
        new ProposedSurface("Layout split pane", NodeKey.From("feature:layout.split-pane"), Kind: null, Parent: null, Evidence: null, Quote: null),
        new ProposedSurface("Restore session", _restore, NodeKind.Feature, Parent: null, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/src/tabs/restore.ts"))], "reopen the tabs I had"),
        new ProposedSurface("Something vague", Key: null, Kind: null, Parent: null, Evidence: null, Quote: null),
    ]);

    private async Task<ProposeResult> Propose(ScopeProposal? proposal = null, ContextScope? scope = null) =>
        (await _world.Map.Propose(proposal ?? Run(), scope ?? MapWorld.Human, MapWorld.None)).Value;

    private async Task<int> ProposeRows() => (await _world.Store.Overrides(MapWorld.None)).Value.Count(o => o.Action == OverrideAction.Propose);

    [Fact, Trait("Lane", "unit")]
    public async Task A_proposal_with_no_map_at_all_is_SND_CONTEXT_030()
    {
        var result = await _world.Map.Propose(Run(), MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.NoMap.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Proposing_opens_a_draft_as_a_copy_of_current_and_writes_one_row_for_the_new_surface()
    {
        var current = (await _world.ImportFixture()).Version;

        var result = await Propose();

        Assert.Equal((1, 1, 0), (result.Added, result.Existing, result.Rejected));   // the mapped node, the vague surface (no row either way)
        Assert.Equal(MapVersionStatus.Draft, result.Draft.Status);
        var versions = (await _world.Store.Versions(MapWorld.None)).Value;
        Assert.Equal(current.Number, Assert.Single(versions, v => v.Status == MapVersionStatus.Current).Number);
        var row = Assert.Single((await _world.Store.Overrides(MapWorld.None)).Value, o => o.Action == OverrideAction.Propose);
        Assert.Equal((_restore.Value, "service:engine", "run-1"), (row.Target.Value, row.Who, row.Proposal!.Run));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Proposing_never_changes_the_current_map()
    {
        var current = (await _world.ImportFixture()).Version;
        var before = (await _world.Store.Read(current.Number, MapWorld.None)).Value;

        _ = await Propose();

        var after = (await _world.Store.Read(current.Number, MapWorld.None)).Value;
        Assert.Equal((before.Nodes.Count, before.Edges.Count), (after.Nodes.Count, after.Edges.Count));
        Assert.Null(await CurrentOnlyResolves(_restore));
    }

    private async Task<GraphNode?> CurrentOnlyResolves(NodeKey key)
    {
        var lenses = await _world.Engine.ShowNode(key.Value, MapWorld.Human, MapWorld.None);
        return lenses.IsSuccess ? new GraphNode(key, "found") : null;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Proposing_the_same_run_again_is_idempotent()
    {
        _ = await _world.ImportFixture();
        _ = await Propose();

        var again = await Propose();

        Assert.Equal((0, 2, 0), (again.Added, again.Existing, again.Rejected));
        Assert.Equal(1, await ProposeRows());
        _ = await Propose(Run("run-2"));
        Assert.Equal(1, await ProposeRows());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rejected_proposal_is_not_proposed_again()
    {
        _ = await _world.ImportFixture();
        _ = await Propose();
        _world.Clock.Advance(TimeSpan.FromMinutes(1));   // rows apply in time order: the reject comes after the proposal
        var decision = new ReviewDecision(ReviewVerb.Reject, _restore);
        Assert.True((await _world.Map.ApplyReview(decision, MapWorld.Human, MapWorld.None)).IsSuccess);

        var again = await Propose();

        Assert.Equal((0, 1, 1), (again.Added, again.Existing, again.Rejected));
        Assert.Equal(1, await ProposeRows());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_proposal_joins_the_latest_draft_and_rides_over_a_rebuild()
    {
        _ = await _world.ImportFixture();
        _ = await Drafts.Build(_world);

        var result = await Propose();
        _ = await Drafts.Build(_world);

        var queue = (await _world.Map.ReviewQueue(MapWorld.Human, MapWorld.None)).Value;
        Assert.Equal(MapVersionStatus.Draft, result.Draft.Status);
        Assert.Contains(queue.Entries, e => e.Node.Value == _restore.Value && e.Group == ReviewGroup.Model);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("nope")]
    [InlineData("other")]
    public async Task A_repo_the_caller_cant_address_is_SND_CONTEXT_045_with_one_answer_for_every_reason(string repo)
    {
        _ = await _world.ImportFixture();
        var proposal = Run() with { Repo = ContextRepoName.From(repo) };
        var onlyFixture = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);

        var human = await _world.Map.Propose(proposal, onlyFixture, MapWorld.None);
        var session = await _world.Map.Propose(proposal, MapWorld.Session(FixtureMap.Repo), MapWorld.None);

        Assert.Equal((ContextErrors.RepoOutsideScope.Code, ContextErrors.RepoOutsideScope.Code), (human.Error.Code, session.Error.Code));
        Assert.Equal(human.Error.Message, session.Error.Message);
        Assert.Equal(0, await ProposeRows());
    }
}
