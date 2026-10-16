using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Deciding from the row and the decision sheet (U3' T6): A and X send Decide once with the option, a refusal shows as served.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellDecideTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.SpecShaped("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> WithDecision()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        for (var i = 0; i < 400 && shell.State != ShellState.Normal; i++)
        {
            await Task.Delay(5, Token);
        }

        shell.ShowNeedsYou(new NeedsYouFeed([NeedsYouRows.Decision()], ""));
        _ = shell.Press("J");
        return shell;
    }

    [Fact]
    public async Task X_on_a_decision_row_sends_Decide_with_the_rejectLike_option_once()
    {
        var shell = await WithDecision();

        Assert.True(shell.Press("X"));
        await shell.Decision.Settled;
        Assert.True(shell.Press("X"));
        await shell.Decision.Settled;

        Assert.Equal([("DEC-1", "decline")], [.. _server.DecideCalls.Select(c => (c.Decision, c.Request.Choice))]);
    }

    [Fact]
    public async Task A_sends_nothing_until_the_decisions_sheet_is_open()
    {
        var shell = await WithDecision();

        Assert.False(shell.Press("A"));
        await shell.Decision.Settled;

        Assert.Empty(_server.DecideCalls);
    }

    [Fact]
    public async Task A_with_the_sheet_open_accepts_the_recommended_option()
    {
        var shell = await WithDecision();
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));

        Assert.True(shell.Press("A"));
        await shell.Decision.Settled;

        Assert.Equal([("DEC-1", "accept")], [.. _server.DecideCalls.Select(c => (c.Decision, c.Request.Choice))]);
        Assert.NotEmpty(shell.Decision.Outcome);
    }

    [Fact]
    public async Task The_sheet_has_one_button_per_option_as_served_and_the_first_is_recommended()
    {
        var shell = await WithDecision();

        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));

        Assert.Equal([("Accept", true), ("Decline", false)], [.. shell.Decision.Options.Select(o => (o.Label, o.IsRecommended))]);
        Assert.Equal("Re-scope: drop the iOS head from this item", shell.Decision.Question);
    }

    [Fact]
    public async Task An_option_press_sends_the_option_and_the_note()
    {
        var shell = await WithDecision();
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        shell.Decision.Note = "agreed in standup";

        await shell.Decision.Choose("decline");

        var (_, request) = Assert.Single(_server.DecideCalls);
        Assert.Equal(("decline", "agreed in standup"), (request.Choice, request.Note));
    }

    [Fact]
    public async Task A_conflict_shows_the_served_detail_inline_and_opens_the_sheet()
    {
        var shell = await WithDecision();
        _server.DecideAnswers["DEC-1"] = RouteAnswer.SpecShaped("decisions-id", "decided-elsewhere");

        Assert.True(shell.Press("X"));
        await shell.Decision.Settled;

        Assert.NotEmpty(shell.Decision.Refusal);
        Assert.True(shell.IsSheetOpen);
        _ = Assert.Single(_server.DecideCalls);
    }

    [Fact]
    public async Task A_row_that_clears_while_the_sheet_is_open_disables_the_form_and_keeps_its_text()
    {
        var shell = await WithDecision();
        shell.Navigate(new DeepLink(DeepLinkKind.Decision, "DEC-1"));
        shell.Decision.Note = "half written";

        shell.ShowNeedsYou(new NeedsYouFeed([], ""));

        Assert.True(shell.Decision.IsCleared);
        Assert.All(shell.Decision.Options, o => Assert.False(o.IsEnabled));
        Assert.Equal("half written", shell.Decision.Note);
        Assert.Equal("Re-scope: drop the iOS head from this item", shell.Decision.Question);
    }
}
