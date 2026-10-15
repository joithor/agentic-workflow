using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Items;

/// <summary>The views a query test reads, seeded through the real projections: items created and moved at stepped times.</summary>
internal sealed class ItemQueryHarness
{
    public ViewsHarness Views { get; } = new();

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The id with 32 copies of <paramref name="digit"/>.</summary>
    public static ItemId Id(char digit) => ItemId.From("ITM-" + new string(digit, 32));

    /// <summary>Creates an item one minute after the last event, in <paramref name="state"/> when not new.</summary>
    public async Task Item(char digit, string title, string? repo = null, ItemState state = ItemState.New)
    {
        Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        var id = Id(digit);
        await Views.Fold(EventTypes.ItemCreated, new ItemCreated(Views.Slice.Ids.New<CommandId>(), id, title, repo is null ? null : RepoName.From(repo), Link: null, Body: "the body", Owner: null), id);
        if (state != ItemState.New)
        {
            await Move(digit, state);
        }
    }

    /// <summary>Moves an item one minute after the last event.</summary>
    public async Task Move(char digit, ItemState to)
    {
        Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        await Views.Fold(EventTypes.ItemStateChanged, new ItemStateChanged(Views.Slice.Ids.New<CommandId>(), Id(digit), ItemState.New, to, 2, Reason: null), Id(digit));
    }

    public IDisposable Human() => Views.Slice.Callers.Enter(A.Caller().Human().Build());

    public IDisposable Session(string repo) => Views.Slice.Callers.Enter(A.Caller().Session().InRepo(repo).Build());
}
