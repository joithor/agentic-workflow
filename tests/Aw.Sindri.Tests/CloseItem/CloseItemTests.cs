using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Features.CloseItem;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.CloseItem;

/// <summary>Closing an item (API spec §3.2, §2.1): 'item.state-changed' at the version the human saw, keyed by the version it moves to.</summary>
[Lane(TestLane.Unit)]
public sealed class CloseItemTests
{
    private static readonly ItemAlias _alias = ItemAlias.From("ENG-142");
    private readonly ViewsHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private CloseItemService Service() => new(new ViewItemResolver(_h.Views), _h.Slice.Store, _h.Slice.Held, _h.Slice.Events, _h.Slice.Callers);

    private async Task Seed()
    {
        _ = await _h.Slice.HoldLease();
        await _h.Fold(EventTypes.ItemCreated, new ItemCreated(_h.Slice.Ids.New<CommandId>(), A.FirstItemId, "Ship it", Repo: null, Link: null, Body: null, Owner: null));
    }

    private IDisposable Human() => _h.Slice.Callers.Enter(A.Caller().Human().Build());

    [Fact]
    public async Task CloseItem_closes_the_item_at_the_version_the_human_saw()
    {
        await Seed();
        using var entered = Human();

        var result = await Service().Handle(A.CloseItemRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        Expect.Equal(EventTypes.ItemStateChanged, recorded.Envelope.Type);
        Expect.Equal(recorded.Sequence, result.Value.Sequence);
        Expect.Equal(A.FirstItemId.Value, recorded.Item);
        Expect.Equal($"item.state-changed:{A.FirstItemId.Value}:v2", recorded.IdempotencyKey);
        var payload = _h.Slice.Events.Payload<ItemStateChanged>(recorded).Value;
        Expect.Equal((ItemState.New, ItemState.Done, 2L), (payload.From, payload.To, payload.Version));
        Expect.Equal(payload.CommandId, result.Value.CommandId);
    }

    [Fact]
    public async Task Cancelled_closes_to_cancelled()
    {
        await Seed();
        using var entered = Human();

        var result = await Service().Handle(A.CloseItemRequest().WithReason(CloseReason.Cancelled).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal(ItemState.Cancelled, _h.Slice.Events.Payload<ItemStateChanged>(Assert.Single(_h.Slice.Store.Events)).Value.To);
    }

    [Fact]
    public async Task Close_by_alias()
    {
        await Seed();
        await _h.Fold(EventTypes.ItemLinked, new ItemLinked(_h.Slice.Ids.New<CommandId>(), A.FirstItemId, RecordKey.From("linear:issue/abc-123"), _alias));
        using var entered = Human();

        var result = await Service().Handle(A.CloseItemRequest().WithItem(ItemRef.To(_alias)).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal(A.FirstItemId.Value, Assert.Single(_h.Slice.Store.Events).Item);
    }

    [Fact]
    public async Task Stale_version_is_SND_ITEM_409_and_records_nothing()
    {
        await Seed();
        using var entered = Human();

        var result = await Service().Handle(A.CloseItemRequest().WithVersion(7).Build(), Token);

        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Unknown_item_is_SND_ITEM_404()
    {
        await Seed();
        using var entered = Human();

        var result = await Service().Handle(A.CloseItemRequest().WithItem(ItemRef.To(ItemId.From("ITM-" + new string('9', 32)))).Build(), Token);

        Expect.Equal(SindriErrors.ItemNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Losing_the_fact_key_race_is_SND_ITEM_409()
    {
        await Seed();
        var other = _h.Slice.Ids.New<CommandId>();
        var rival = new ItemStateChanged(other, A.FirstItemId, ItemState.New, ItemState.Cancelled, 2, Reason: null);
        _ = await _h.Slice.Store.Append(_h.Slice.Events.Entry(EventTypes.ItemStateChanged, rival, $"item.state-changed:{A.FirstItemId.Value}:v2", A.FirstItemId.Value), _h.Slice.Held.Current!, Token);
        using var entered = Human();

        var result = await Service().Handle(A.CloseItemRequest().Build(), Token);

        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
        _ = Assert.Single(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Without_a_lease_it_is_not_running()
    {
        using var entered = Human();

        var result = await Service().Handle(A.CloseItemRequest().Build(), Token);

        Expect.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
    }

    [Fact]
    public void The_cancelled_reason_is_its_own_catalog_variant()
    {
        Expect.Equal("done", A.CloseItemRequest().Build().Variant);
        Expect.Equal("cancelled", A.CloseItemRequest().WithReason(CloseReason.Cancelled).Build().Variant);
    }

    [Fact]
    public void An_undefined_reason_is_refused_by_the_validator_and_has_no_variant()
    {
        var request = A.CloseItemRequest().WithReason((CloseReason)7).Build();

        Expect.Equal(SindriErrors.InvalidInput.Code, new CloseItemValidator().Validate(request).Error.Code);
        Expect.Equal("undefined", request.Variant);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void A_version_below_one_is_invalid_input_not_a_conflict(long version)
    {
        var result = new CloseItemValidator().Validate(A.CloseItemRequest().WithVersion(version).Build());

        Expect.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }
}
