using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <c>ITM-...</c> item asked of the context query becomes the tracker record it is linked to (spec §8.1 item shape; plan S5).</summary>
public sealed class ItemKeysTests
{
    private static readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));
    private static readonly CallerContext _caller = CallerContext.Service("test", new SequentialIdGenerator());

    private static ItemRow Row(RecordKey? link) => new(_item, "t", Repo: null, ItemState.New, Step: null, Version: 1, "human", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch, link, Alias: null, Body: null, WaitingOn: null, Instance: null, Owner: null, Sequence: 1);

    [Fact, Trait("Lane", "unit")]
    public async Task A_linked_item_id_asks_for_its_record()
    {
        var resolved = await ItemKeys.Resolve(_item.Value, new Items(Row(RecordKey.From("linear:issue/abc"))), _caller, TestContext.Current.CancellationToken);

        Assert.Equal("linear:issue/abc", resolved.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_item_id_with_no_record_answers_003_that_says_it_is_not_linked_yet()
    {
        var resolved = await ItemKeys.Resolve(_item.Value, new Items(Row(link: null)), _caller, TestContext.Current.CancellationToken);

        Assert.Equal("SND-CONTEXT-003", resolved.Error.Code);
        Assert.Contains("has no tracker record linked yet", resolved.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("name the context-sync service", resolved.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SPL-103")]
    [InlineData("ITM-00000000000000000000000000000009")]
    public async Task A_tracker_key_or_an_item_the_caller_cannot_see_goes_on_unchanged(string item)
    {
        var resolved = await ItemKeys.Resolve(item, new Items(row: null), _caller, TestContext.Current.CancellationToken);

        Assert.Equal(item, resolved.Value);   // the engine answers its own 003 for an unknown key
    }

    private sealed class Items(ItemRow? row) : IItemResolver
    {
        public ValueTask<Result<ItemRow>> Resolve(ItemRef reference, CallerContext caller, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ItemRow>>(row is not null && reference.Id == row.Id ? row : SindriErrors.ItemNotFound.With("no item"));
    }
}
