using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>A decision refused on the item sheet can be pressed again (review run 1, K6 / corr-7): "never retried" is the app's rule, not the human's.</summary>
[Lane(TestLane.Unit)]
public sealed class ItemDecideRetryTests
{
    private const string DecisionItem = "ITM-0000000000000000000000000000000e";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task A_decide_the_views_could_not_take_yet_can_be_pressed_again() => UiThread.Run(async () =>
    {
        _server.DecisionsByItem[DecisionItem] = RouteAnswer.Api("decisions", "by-item");
        _server.RequestsByItem[DecisionItem] = RouteAnswer.Api("requests", "list-by-item");
        _server.Items[DecisionItem] = _server.Items[A.NewItem];
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, (await _server.Commands(_api, Token)).Value);
        var sheet = new ItemSheetViewModel(_server, catalog, _clock);
        await sheet.Open(_api, DecisionItem, Token);
        var decision = sheet.Needs.Decisions[0];
        _server.Decided = RouteAnswer.Problem("decisions-id", "while-stalled");

        await sheet.Needs.Choose(decision.Id, "accept");

        Assert.All(sheet.Needs.Decisions[0].Options, o => Assert.True(o.IsEnabled));
        _server.Decided = RouteAnswer.Api("decisions-id", "accepted");
        await sheet.Needs.Choose(decision.Id, "accept");
        Assert.Equal(2, _server.DecideCalls.Count);
        Assert.Equal(string.Empty, sheet.Needs.Refusal);
    }, Token);
}
