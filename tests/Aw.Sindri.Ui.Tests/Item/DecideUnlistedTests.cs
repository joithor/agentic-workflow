using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>Decide with no catalog row says something true (review run 4, a11y): never "arrives in" with no phase after it, on the item sheet and the decision sheet.</summary>
[Lane(TestLane.Unit)]
public sealed class DecideUnlistedTests
{
    private const string DecisionItem = "ITM-0000000000000000000000000000000e";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static T Golden<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.Api(slug, scenario)), SindriApiClient.Json).Value;

    private async Task<ItemSheetViewModel> Sheet(CatalogViewModel catalog)
    {
        _server.DecisionsByItem[DecisionItem] = RouteAnswer.Api("decisions", "by-item");
        _server.RequestsByItem[DecisionItem] = RouteAnswer.Api("requests", "list-by-item");
        _server.Items[DecisionItem] = _server.Items[A.NewItem];
        var sheet = new ItemSheetViewModel(_server, catalog, _clock);
        await sheet.Open(_api, DecisionItem, Token);
        return sheet;
    }

    private static DecisionSheetViewModel DecisionSheet(CommandList catalog, FakeTimeProvider clock)
    {
        var pending = Golden<DecisionList>("decisions", "pending");
        var list = Golden<NeedsYouList>("needs-you", "fixture");
        var decision = pending.Decisions[0] with { Id = list.Rows[0].Subject!.Id };
        var entry = NeedsYouMapper.Map(list, catalog, pending with { Decisions = [decision] }, new HomeCounts(0, 0, 0)).Rows[0];
        var sheet = new DecisionSheetViewModel(new FakeSindriApi(), () => null, clock, new DecisionSender(new FakeSindriApi()));
        sheet.Show(new NeedsYouRowViewModel(entry, seen: true, reducedMotion: false, arrived: false, clock));
        return sheet;
    }

    [Fact]
    public Task An_unread_catalog_says_it_is_checking_on_the_item_sheet() => UiThread.Run(async () =>
    {
        var sheet = await Sheet(new CatalogViewModel(new FakeClipboard(), _clock));

        var row = sheet.Needs.Decisions[0];
        Assert.Equal("Checking what can run", row.DecideLine);
        Assert.All(row.Options, o => Assert.DoesNotContain("arrives in", o.Reason, StringComparison.Ordinal));
    }, Token);

    [Fact]
    public Task A_catalog_without_Decide_gives_the_served_command_on_the_item_sheet() => UiThread.Run(async () =>
    {
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        var listed = (await _server.Commands(_api, Token)).Value;
        catalog.Show(_api, _server, new CommandList([.. listed.Rows.Where(r => !string.Equals(r.Action, "Decide", StringComparison.Ordinal))]));
        var sheet = await Sheet(catalog);

        var row = sheet.Needs.Decisions[0];
        Assert.DoesNotContain("arrives in", row.DecideLine, StringComparison.Ordinal);
        Assert.Contains("sindri decide", row.DecideLine, StringComparison.Ordinal);
        Assert.All(row.Options, o => Assert.DoesNotContain("arrives in", o.Reason, StringComparison.Ordinal));
    }, Token);

    [Fact]
    public void An_unread_catalog_says_it_is_checking_on_the_decision_sheet()
    {
        var sheet = DecisionSheet(new CommandList([]), _clock);

        Assert.Equal("Checking what can run", sheet.DecideLine);
    }

    [Fact]
    public void A_catalog_without_Decide_gives_the_served_command_on_the_decision_sheet()
    {
        var listed = Golden<CommandList>("commands", "catalog");
        var sheet = DecisionSheet(new CommandList([.. listed.Rows.Where(r => !string.Equals(r.Action, "Decide", StringComparison.Ordinal))]), _clock);

        Assert.DoesNotContain("arrives in", sheet.DecideLine, StringComparison.Ordinal);
        Assert.Contains("sindri", sheet.DecideLine, StringComparison.Ordinal);
    }
}
