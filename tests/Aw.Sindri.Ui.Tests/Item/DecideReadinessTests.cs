using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Item;

/// <summary>A disabled decision option says why (review run 3, a11y-r3-0): the reason, the phase and the command, on the item sheet and the decision sheet (UI spec §7).</summary>
[Lane(TestLane.Unit)]
public sealed class DecideReadinessTests
{
    private const string DecisionItem = "ITM-0000000000000000000000000000000e";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog") };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<CommandList> Catalog(string decideUi) =>
        new([.. (await _server.Commands(_api, Token)).Value.Rows.Select(r => string.Equals(r.Action, "Decide", StringComparison.Ordinal) ? r with { Ui = decideUi } : r)]);

    private async Task<(ItemSheetViewModel Sheet, CatalogViewModel Catalog)> Sheet(string decideUi)
    {
        _server.DecisionsByItem[DecisionItem] = RouteAnswer.Api("decisions", "by-item");
        _server.RequestsByItem[DecisionItem] = RouteAnswer.Api("requests", "list-by-item");
        _server.Items[DecisionItem] = _server.Items[A.NewItem];
        var catalog = new CatalogViewModel(new FakeClipboard(), _clock);
        catalog.Show(_api, _server, await Catalog(decideUi));
        var sheet = new ItemSheetViewModel(_server, catalog, _clock);
        await sheet.Open(_api, DecisionItem, Token);
        return (sheet, catalog);
    }

    [Fact]
    public Task Pending_Decide_disables_the_item_options_with_the_command_and_a_line_naming_the_phase() => UiThread.Run(async () =>
    {
        var (sheet, _) = await Sheet("pending:P4");

        var row = sheet.Needs.Decisions[0];
        Assert.All(row.Options, o => Assert.False(o.IsEnabled));
        Assert.All(row.Options, o => Assert.Contains("sindri decide", o.Reason, StringComparison.Ordinal));
        Assert.Contains("P4", row.DecideLine, StringComparison.Ordinal);
        Assert.Contains("sindri decide", row.DecideLine, StringComparison.Ordinal);
    }, Token);

    [Fact]
    public Task Cli_only_Decide_says_it_needs_a_terminal() => UiThread.Run(async () =>
    {
        var (sheet, _) = await Sheet("cli-only:typed-confirmation");

        var row = sheet.Needs.Decisions[0];
        Assert.All(row.Options, o => Assert.Equal(Labels.ProtectedTooltip, o.Reason));
        Assert.StartsWith(Labels.ProtectedTooltip, row.DecideLine, StringComparison.Ordinal);
        Assert.Contains("sindri decide", row.DecideLine, StringComparison.Ordinal);   // the served per-option command follows the sentence
    }, Token);

    [Fact]
    public Task The_options_follow_the_catalog_when_it_arrives_after_the_decisions() => UiThread.Run(async () =>
    {
        var (sheet, catalog) = await Sheet("pending:P4");
        Assert.All(sheet.Needs.Decisions[0].Options, o => Assert.False(o.IsEnabled));

        catalog.Show(_api, _server, await Catalog("ready"));

        var row = sheet.Needs.Decisions[0];
        Assert.All(row.Options, o => Assert.True(o.IsEnabled));
        Assert.All(row.Options, o => Assert.Equal(string.Empty, o.Reason));
        Assert.Equal(string.Empty, row.DecideLine);
    }, Token);

    [Fact]
    public void The_decision_sheet_options_carry_the_reason_and_the_line_names_the_phase()
    {
        var served = NeedsYouRows.Decision();
        var pending = served with { Actions = [.. served.Actions.Select(a => a with { Readiness = ActionReadiness.Pending, Cli = "sindri decide DEC-1 " + a.OptionId, Stage = "P4" })] };
        var sheet = new DecisionSheetViewModel(new FakeSindriApi(), () => null, _clock, new DecisionSender(new FakeSindriApi()));

        sheet.Show(new NeedsYouRowViewModel(pending, seen: true, reducedMotion: false, arrived: false, _clock));

        Assert.All(sheet.Options, o => Assert.False(o.IsEnabled));
        Assert.Equal(["sindri decide DEC-1 accept", "sindri decide DEC-1 decline"], sheet.Options.Select(o => o.Reason), StringComparer.Ordinal);
        Assert.Contains("P4", sheet.DecideLine, StringComparison.Ordinal);
        Assert.Contains("sindri decide DEC-1 accept", sheet.DecideLine, StringComparison.Ordinal);
    }
}
