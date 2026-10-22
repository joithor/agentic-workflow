using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Controls;
using Aw.Sindri.Ui.ViewModels.Confirmations;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>
/// Grant tokens in the app (per-round budgets brief, 2026-10-21): the amount starts at the served suggestion, the shared hold or typed confirmation
/// answers a challenge bound to the amount and the instance, and the grant is the one 'POST …/commands' with that challenge. While the catalog's row
/// is not ready the control is the terminal copy of the served command.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GrantTokensTests
{
    private const string Address = "ITM-01JH5/step/1";
    private const string Served = "sindri workflow answer ITM-01JH5/step/1 --grant-tokens 1M";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly AppConfirmFixture _f = new();
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GrantTokensViewModel Grant(string ui = "ready") => new(_server, _f.Catalog(ui), _f.Clock, () => _api, ("WFI-44393da815f188cea9c6fe30ae2a30a6", Address), Served);

    private void Serves(string action, int minHoldMs = 1500) => _server.AppChallengeAnswer = new RouteAnswer(HttpStatusCode.OK,
        string.Create(CultureInfo.InvariantCulture, $$"""{"challenge":"CHL-00000000000000000000000000000003","action":"{{action}}","expiresAt":"2026-01-01T00:05:00+00:00","confirm":"held","minHoldMs":{{minHoldMs}},"typedAlternative":"{{action}}"}"""));

    [Fact]
    public void The_app_confirms_the_token_grant_itself()
    {
        Assert.Equal(AppConfirm.GrantTokens, AppConfirm.Require(AppConfirm.GrantTokens));
        Assert.Contains(AppConfirm.GrantTokens, AppConfirm.Actions, StringComparer.Ordinal);
    }

    [Fact]
    public void An_action_the_app_does_not_confirm_is_still_refused() =>
        Assert.Throws<ArgumentException>(() => AppConfirm.Require("ApplyCommand:answer-accept"));

    [Fact]
    public void The_catalog_row_decides_whether_the_app_confirms_or_the_terminal_copy_stands_in()
    {
        var ready = Grant("ready");
        var notReady = Grant("cli-only:typed-confirmation");

        Assert.Equal((true, null), (ready.IsReady, ready.Terminal));
        Assert.False(notReady.IsReady);
        Assert.Equal(Served, notReady.Terminal!.Cli);   // the lock button copies the command the engine served for this instance
    }

    [Fact]
    public void The_amount_starts_at_the_suggestion_the_park_served()
    {
        var grant = Grant();

        Assert.Equal(("1M", 1_000_000), (grant.Amount, grant.AmountTokens));
    }

    [Theory]
    [InlineData("500k", 500_000)]
    [InlineData("2M", 2_000_000)]
    [InlineData("750000", 750_000)]
    public void The_amount_reads_as_recipes_write_it(string text, int tokens)
    {
        var grant = Grant();

        grant.Amount = text;

        Assert.Equal(tokens, grant.AmountTokens);
    }

    [Theory]
    [InlineData("")]
    [InlineData("lots")]
    [InlineData("0")]
    [InlineData("-5k")]
    [InlineData("99999999999M")]
    public void An_amount_that_is_not_tokens_says_so_and_cannot_be_held(string text)
    {
        var grant = Grant();

        grant.Amount = text;

        Assert.Null(grant.AmountTokens);
        Assert.Equal(text.Trim().Length > 0, grant.ShowsAmountProblem);
        Assert.False(grant.Hold.Begin());
    }

    [Fact]
    public async Task The_challenge_is_asked_for_the_amount_and_the_instance()
    {
        Serves("grant-tokens 1000000 " + Address);
        var grant = Grant();

        await grant.Prepare();

        Assert.Equal("grant-tokens 1000000 " + Address, _server.Challenges.Single().Action);
        Assert.Equal("grant-tokens 1000000 " + Address, grant.TypedBox.Phrase);
    }

    [Fact]
    public async Task A_complete_hold_sends_the_grant_with_the_time_held_once()
    {
        Serves("grant-tokens 1000000 " + Address);
        var grant = Grant();
        var granted = 0;
        grant.Granted += (_, _) => granted++;
        await grant.Prepare();

        Assert.True(grant.Hold.Begin());
        _f.Clock.Advance(TimeSpan.FromMilliseconds(1500));
        await grant.Tick();

        var (instance, sent) = _server.CommandCalls.Single();
        Assert.Equal(("WFI-44393da815f188cea9c6fe30ae2a30a6", CommandKind.Answer, AnswerVariant.GrantTokens, 1_000_000, 1500L), (instance, sent.Kind, sent.Answer, sent.Amount, sent.Challenge!.HeldMs));
        Assert.Equal((Address, "CHL-00000000000000000000000000000003"), (sent.Instance, sent.Challenge!.Challenge));
        Assert.Equal((1, true), (granted, grant.IsDone));
    }

    [Fact]
    public async Task A_hold_that_is_let_go_early_sends_nothing()
    {
        Serves("grant-tokens 1000000 " + Address);
        var grant = Grant();
        await grant.Prepare();
        _ = grant.Hold.Begin();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(900));

        await grant.Tick();
        grant.CancelHold();

        Assert.Empty(_server.CommandCalls);
        Assert.False(grant.IsDone);
    }

    [Fact]
    public async Task The_exact_typed_text_sends_the_grant_and_other_text_sends_nothing()
    {
        Serves("grant-tokens 1000000 " + Address);
        var grant = Grant();
        await grant.Prepare();

        grant.TypedBox.Text = "grant-tokens 1 " + Address;
        await grant.SubmitTyped();
        Assert.Empty(_server.CommandCalls);

        grant.TypedBox.Text = "grant-tokens 1000000 " + Address;
        await grant.SubmitTyped();

        var sent = _server.CommandCalls.Single().Body;
        Assert.Equal(("grant-tokens 1000000 " + Address, null), (sent.Challenge!.Typed, sent.Challenge.HeldMs));
    }

    [Fact]
    public async Task Changing_the_amount_drops_the_challenge_and_the_next_press_asks_for_a_new_one_before_it_holds()
    {
        Serves("grant-tokens 1000000 " + Address);
        var grant = Grant();
        await grant.Prepare();

        grant.Amount = "500k";
        Serves("grant-tokens 500000 " + Address);
        var firstPress = grant.Hold.Begin();
        await grant.Prepare();
        var secondPress = grant.Hold.Begin();

        Assert.Equal((false, true), (firstPress, secondPress));
        Assert.Equal("grant-tokens 500000 " + Address, grant.TypedBox.Phrase);
    }

    [Fact]
    public async Task A_refusal_is_shown_in_the_servers_words_and_the_spent_challenge_is_gone()
    {
        Serves("grant-tokens 1000000 " + Address);
        _server.CommandAnswer = RouteAnswer.Problem(HttpStatusCode.UnprocessableEntity, "SND-WF-003", "--grant-tokens 1M is outside 1..400k, 4x the original budget of 100k (fix: use --grant-tokens 1 to --grant-tokens 400k)");
        var grant = Grant();
        await grant.Prepare();
        _ = grant.Hold.Begin();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(1500));

        await grant.Tick();

        Assert.Contains("outside 1..400k", grant.Error, StringComparison.Ordinal);
        Assert.False(grant.IsDone);
        Assert.False(grant.Hold.Begin());   // the challenge was used: a new press asks for another
    }

    [Fact]
    public async Task The_frame_shows_the_control_on_a_budget_park_and_gives_the_park_back_once_granted()
    {
        var node = GoldenFiles.Node("workflow-instance", "item-produce");
        node["current"]!["status"]!["answer"] = new JsonObject
        {
            ["kind"] = "grantTokens",
            ["actionId"] = "answer-grant-tokens",
            ["class"] = "protected",
            ["cli"] = "sindri workflow answer ITM-01JE7/item/1/step.4 --grant-tokens 1M",
            ["atTerminal"] = false,
        };
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var frame = new GrantFrame(_server, _f).Frame;

        await frame.Open(_api, (string)node["address"]!, (string)node["id"]!, Token);

        Assert.Equal("1M", frame.Grant!.Suggested);
        Assert.True(frame.Grant.IsReady);
    }

    [Fact]
    public async Task The_frame_shows_no_control_without_a_token_grant_answer()
    {
        var node = GoldenFiles.Node("workflow-instance", "item-produce");
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var frame = new GrantFrame(_server, _f).Frame;

        await frame.Open(_api, (string)node["address"]!, (string)node["id"]!, Token);

        Assert.Null(frame.Grant);
    }

    private sealed class GrantFrame(FakeSindriApi server, AppConfirmFixture f)
    {
        internal InstanceFrameViewModel Frame { get; } = new(server, f.Clipboard, f.Clock, viewers: null, f.Catalog("ready"));
    }
}
