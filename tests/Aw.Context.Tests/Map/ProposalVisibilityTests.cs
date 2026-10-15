using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

public sealed class ProposalVisibilityTests : IDisposable
{
    private static readonly NodeKey _restore = NodeKey.From("feature:tabs.restore-session");
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static MapOverride Pending() => new(OverrideAction.Propose, _restore, "service:engine",
        DateTimeOffset.Parse("2026-10-14T20:00:00Z", System.Globalization.CultureInfo.InvariantCulture), "Restore session",
        Proposal: new ProposalPayload("run-1", ContextRepoName.From(FixtureMap.Repo), NodeKind.Feature));

    [Fact, Trait("Lane", "unit")]
    public async Task A_pending_proposal_is_not_a_node_of_the_current_map()
    {
        _ = await _world.ImportFixture();
        _ = await _world.Store.AppendOverrides([Pending()], MapWorld.None);

        var shown = await _world.Engine.ShowNode(_restore.Value, MapWorld.Human, MapWorld.None);
        var status = (await _world.Map.MapStatus(anchor: null, MapWorld.Human, MapWorld.None)).Value;

        Assert.Equal(ContextErrors.UnknownAnchor.Code, shown.Error.Code);
        Assert.Equal(0, status.Orphaned);
    }
}
