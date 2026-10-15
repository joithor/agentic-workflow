using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;
using Aw.Context.Tests.ReviewMap;

namespace Aw.Context.Tests.Proposals;

public sealed class GetProposalsTests : IDisposable
{
    private static readonly NodeKey _restore = NodeKey.From("feature:tabs.restore-session");
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ScopeProposal Run(string run = "run-1") => new(run, ContextRepoName.From(FixtureMap.Repo),
    [new ProposedSurface("Restore session", _restore, NodeKind.Feature, Parent: null, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/src/tabs/restore.ts"))], Quote: null)]);

    private async Task<ProposalsView> Proposals(string run = "run-1", ContextScope? scope = null) =>
        (await _world.Map.Proposals(run, scope ?? MapWorld.Human, MapWorld.None)).Value;

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_map_it_is_SND_CONTEXT_030()
    {
        var view = await _world.Map.Proposals("run-1", MapWorld.Human, MapWorld.None);

        Assert.Equal(ContextErrors.NoMap.Code, view.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_that_proposed_nothing_has_no_surfaces()
    {
        _ = await _world.ImportFixture();

        var view = await Proposals("never-ran");

        Assert.Empty(view.Surfaces);
        Assert.Null(view.Draft);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_proposed_surface_is_new_while_only_the_draft_has_it()
    {
        _ = await _world.ImportFixture();
        var proposed = (await _world.Map.Propose(Run(), MapWorld.Human, MapWorld.None)).Value;

        var view = await Proposals();

        var surface = Assert.Single(view.Surfaces);
        Assert.Equal(("Restore session", GraphStatusKind.New, _restore.Value), (surface.Surface, surface.Status, surface.Node!.Value.Value));
        Assert.Equal(("v1", Drafts.Short(proposed.Draft)), (view.Map, view.Draft!.Split('@')[0]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rejected_proposal_is_unmapped()
    {
        _ = await _world.ImportFixture();
        _ = await _world.Map.Propose(Run(), MapWorld.Human, MapWorld.None);
        _world.Clock.Advance(TimeSpan.FromMinutes(1));   // rows apply in time order: the reject comes after the proposal
        Assert.True((await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.Reject, _restore), MapWorld.Human, MapWorld.None)).IsSuccess);

        var view = await Proposals();

        Assert.Equal(GraphStatusKind.Unmapped, Assert.Single(view.Surfaces).Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task After_the_accepted_proposal_is_promoted_it_reads_mapped_without_running_the_scope_again()
    {
        _ = await _world.ImportYaml(MapWorld.Yaml("schemaVersion: 1", "kind: authored", "repo: aw-split-panes", "nodes:", "  - {key: \"feature:a\", label: \"A\"}"));
        _ = await _world.Map.Propose(Run(), MapWorld.Human, MapWorld.None);
        _world.Clock.Advance(TimeSpan.FromMinutes(1));   // rows apply in time order: the accept comes after the proposal
        var accept = new ReviewDecision(ReviewVerb.Accept, _restore);
        Assert.True((await _world.Map.ApplyReview(accept, MapWorld.Human, MapWorld.None)).IsSuccess);

        var promoted = await _world.Map.Promote(MapWorld.Human, MapWorld.None);

        Assert.True(promoted.IsSuccess, promoted.IsError ? promoted.Error.Message : null);
        var view = await Proposals();
        Assert.Equal(GraphStatusKind.Mapped, Assert.Single(view.Surfaces).Status);
        Assert.Null(view.Draft);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_session_sees_only_the_proposals_of_its_own_repo_and_nothing_with_sessions_off()
    {
        _ = await _world.ImportFixture();
        _ = await _world.Map.Propose(Run(), MapWorld.Human, MapWorld.None);

        var own = await Proposals(scope: MapWorld.Session(FixtureMap.Repo));
        var other = await _world.Map.Proposals("run-1", MapWorld.Session("other"), MapWorld.None);
        var off = await _world.Map.Proposals("run-1", MapWorld.Session(FixtureMap.Repo, enabled: false), MapWorld.None);

        _ = Assert.Single(own.Surfaces);
        Assert.Empty(other.Value.Surfaces);   // a profile repo of its own, with no proposal of its own
        Assert.Equal(ContextErrors.SessionsDisabled.Code, off.Error.Code);
    }
}
