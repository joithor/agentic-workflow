using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Proposals;
using Aw.Context.Core.Review;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Proposals;

public sealed class ReproposeAfterUndoTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static ScopeProposal Run() => new("run-1", ContextRepoName.From(FixtureMap.Repo),
        [new ProposedSurface("Restore session", NodeKey.From("feature:tabs.restore-session"), NodeKind.Feature, Parent: null, [new EvidenceRef(RecordKey.From("repo:aw-split-panes/src/tabs/restore.ts"))], Quote: null)]);

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_that_proposes_a_surface_again_after_a_person_undid_its_row_gets_a_new_row_counted_as_added()
    {
        _ = await _world.ImportFixture();
        var first = (await _world.Map.Propose(Run(), MapWorld.Human, MapWorld.None)).Value;
        var undone = await _world.Map.ApplyReview(new ReviewDecision(ReviewVerb.Undo, Undoes: Assert.Single(first.RowsOrEmpty)), MapWorld.Human, MapWorld.None);

        var again = (await _world.Map.Propose(Run(), MapWorld.Human, MapWorld.None)).Value;
        var third = (await _world.Map.Propose(Run(), MapWorld.Human, MapWorld.None)).Value;

        Assert.True(undone.IsSuccess, undone.IsError ? undone.Error.Message : null);
        Assert.Equal((1, 0), (again.Added, again.Existing));
        Assert.NotEqual(first.RowsOrEmpty[0].Value, Assert.Single(again.RowsOrEmpty).Value, StringComparer.Ordinal);   // the undone row isn't counted among the run's rows
        Assert.Equal((0, 1), (third.Added, third.Existing));
        var view = (await _world.Map.Proposals("run-1", MapWorld.Human, MapWorld.None)).Value;
        Assert.Equal(GraphStatusKind.New, Assert.Single(view.Surfaces).Status);
    }
}
