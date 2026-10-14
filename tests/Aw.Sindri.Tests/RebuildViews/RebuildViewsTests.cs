using System.Globalization;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.RebuildViews;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.RebuildViews;

/// <summary>POST /views/rebuild (API spec §3.2): records 'views.rebuilt' (plan G4) and asks the projector to rebuild, never waiting for the views.</summary>
public sealed class RebuildViewsTests
{
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task RebuildViews_appends_views_rebuilt_and_asks_the_projector_to_rebuild()
    {
        await Ledger(events: 3);
        await _v.Fold(EventTypes.ProfileApproved, new ProfileApproved(SliceHarness.Profile().Hash, []));   // the projector at cursor 1 of 3
        var caller = A.Caller().Human().Build();
        using var entered = _v.Slice.Callers.Enter(caller);

        var result = await Service().Handle(A.RebuildViewsRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        var rebuilt = Assert.Single(_v.Slice.Store.Events, e => e.Envelope.Type == EventTypes.ViewsRebuilt);
        Assert.Equal(new ViewsRebuilt(caller.CommandId, CursorBefore: 1, EventsReplayed: 3), _v.Slice.Events.Payload<ViewsRebuilt>(rebuilt).Value);
        Assert.Equal($"views.rebuilt:{caller.CommandId.Value}", rebuilt.IdempotencyKey);
        Assert.Equal((caller.CommandId, rebuilt.Sequence), (result.Value.CommandId, result.Value.Sequence));
        Assert.True(_v.Progress.Current.Rebuilding);
        Assert.True(_v.Progress.TakeRebuildRequest());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_the_lease_it_is_SND_LEASE_002_and_records_nothing()
    {
        using var entered = _v.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new RebuildViewsRequest(), Token);

        Assert.Equal("SND-LEASE-002", result.Error.Code);
        Assert.Empty(_v.Slice.Store.Events);
        Assert.False(_v.Progress.Current.Rebuilding);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rebuild_runs_while_stalled()
    {
        await Ledger(events: 2);
        _v.Publish(stalledAt: 1);
        using var entered = _v.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new RebuildViewsRequest(), Token);

        Assert.True(result.IsSuccess);
        Assert.True(_v.Progress.Current.Rebuilding);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Rebuild_doesnt_wait_for_the_head()
    {
        await Ledger(events: 5);   // the projector never moved: cursor 0 of 5, and the fake clock never advances
        using var entered = _v.Slice.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new RebuildViewsRequest(), Token);

        Assert.Equal(6, result.Value.Sequence);
    }

    private RebuildViewsService Service() =>
        new(_v.Progress, _v.Progress, _v.Slice.Store, _v.Slice.Held, _v.Slice.Events, _v.Slice.Callers, _v.Slice.Ids);

    /// <summary>Holds the lease and appends <paramref name="events"/> service runs.</summary>
    private async Task Ledger(int events)
    {
        var grant = await _v.Slice.HoldLease();
        for (var i = 1; i <= events; i++)
        {
            _ = (await _v.Slice.Store.Append(_v.Slice.Events.Entry(EventTypes.ServiceRan,
                new ServiceRan(_v.Slice.Ids.New<CommandId>(), ServiceName.From("heartbeat"), _v.Slice.Clock.GetUtcNow().AddSeconds(i), ServiceOutcome.Ok, Code: null),
                string.Create(CultureInfo.InvariantCulture, $"service.ran:{i}")), grant, Token)).Value;
        }
    }
}
