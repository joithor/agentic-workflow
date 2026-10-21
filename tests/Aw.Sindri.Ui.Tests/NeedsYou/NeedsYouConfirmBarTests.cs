using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Controls;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.Tests.NeedsYou;

/// <summary>The inbox's typed and held confirmations (needs-you spec §8.3, slice 3): what the bar shows for a drifted profile, approve-all and a changed workflow, and that only the control approves.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouConfirmBarTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly AppConfirmFixture _fixture = new();
    private readonly FakeSindriApi _server = new();
    private int _focusRequests;

    private async Task<NeedsYouBarViewModel> Bar(NeedsYouEntry entry, string ui = "ready")
    {
        var confirm = new NeedsYouConfirm(_fixture.Catalog(ui), () => _api, _server, _fixture.Clock);
        var bar = new NeedsYouBarViewModel(new DeskReporter(_server, () => _api), _fixture.Clipboard, _fixture.Clock, confirm);
        bar.ConfirmFocusRequested += (_, _) => _ = ++_focusRequests;
        bar.Show(new NeedsYouRowViewModel(entry, seen: true, reducedMotion: false, arrived: false, _fixture.Clock));
        await bar.Loaded;
        bar.ReadToEnd = true;
        return bar;
    }

    private void ServeProfileChallenge() => _server.AppChallengeAnswer = RouteAnswer.Api("workflow-challenges-app", "profile");

    [Fact]
    public async Task A_drifted_profile_shows_the_typed_box_with_the_servers_challenge_text_verbatim()
    {
        ServeProfileChallenge();

        var bar = await Bar(NeedsYouRows.ProfileReady());

        var asked = Assert.Single(_server.Challenges);
        Assert.Equal("approve profile 932d87", asked.Action);   // the row's full hash, shortened as the server's challenge is
        Assert.Equal((true, false, false, "approve profile 70e158"), (bar.ShowsTyped, bar.ShowsHold, bar.ShowsTerminal, bar.TypedBox!.Phrase));   // the served text, not the one the app asked with
    }

    [Fact]
    public async Task Typing_the_phrase_and_pressing_sends_the_full_hash_with_the_answer_once_and_approves_the_row()
    {
        ServeProfileChallenge();
        var bar = await Bar(NeedsYouRows.ProfileReady());
        bar.TypedBox!.Text = "approve profile 70e158";

        await bar.SubmitTyped(bar.TypedBox.Submit()!);
        await bar.SubmitTyped("approve profile 70e158");   // a second press while approved sends nothing

        var sent = Assert.Single(_server.ProfileApprovals);
        Assert.Equal((NeedsYouRows.ProfileHash, "CHL-00000000000000000000000000000006", "approve profile 70e158"), (sent.Hash, sent.Answer.Challenge, sent.Answer.Typed));
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task Text_that_is_not_the_phrase_cannot_be_submitted()
    {
        ServeProfileChallenge();
        var bar = await Bar(NeedsYouRows.ProfileReady());
        bar.TypedBox!.Text = "approve profile 000000";

        Assert.Null(bar.TypedBox.Submit());
        Assert.Empty(_server.ProfileApprovals);
    }

    [Fact]
    public async Task A_on_a_typed_row_only_moves_to_the_box()
    {
        ServeProfileChallenge();
        var bar = await Bar(NeedsYouRows.ProfileReady());

        await bar.PressApprove();

        Assert.Equal((1, NeedsYouBarMode.Pending, false), (_focusRequests, bar.Mode, bar.IsApproved));
        Assert.Empty(_server.ProfileApprovals);
    }

    [Fact]
    public async Task A_refused_approval_shows_the_words_and_leaves_the_row_open()
    {
        ServeProfileChallenge();
        _server.ApproveProfileAnswer = RouteAnswer.Problem("profile-approve", "refused-session");
        var bar = await Bar(NeedsYouRows.ProfileReady());
        bar.TypedBox!.Text = "approve profile 70e158";

        await bar.SubmitTyped(bar.TypedBox.Submit()!);

        Assert.Equal((false, true, Labels.ApproveSessionRefused), (bar.IsApproved, bar.HasRefusal, bar.Refusal));
        _ = Assert.Single(_server.Challenges);   // an agent session is refused every time: asking again helps nobody
    }

    [Fact]
    public async Task Approve_all_shows_the_hold_and_the_hold_sends_once_and_approves_the_row()
    {
        var bar = await Bar(NeedsYouRows.SetupRecipes());

        Assert.Equal((true, false, false), (bar.ShowsHold, bar.ShowsTyped, bar.ShowsTerminal));
        Assert.StartsWith("approve-all ", Assert.Single(_server.Challenges).Action, StringComparison.Ordinal);

        await bar.HoldReached(1500);

        Assert.Equal(1500, Assert.Single(_server.Approvals).HeldMs);
        Assert.True(bar.IsApproved);
    }

    [Fact]
    public async Task A_on_the_hold_row_only_moves_to_the_button()
    {
        var bar = await Bar(NeedsYouRows.SetupRecipes());

        await bar.PressApprove();

        Assert.Equal((1, false), (_focusRequests, bar.IsApproved));
        Assert.Empty(_server.Approvals);
    }

    [Fact]
    public async Task A_single_workflows_approve_is_the_terminal_copy_only_even_when_the_catalog_says_ready()
    {
        var bar = await Bar(NeedsYouRows.Recipe());

        Assert.Equal((true, true, false), (bar.HasBar, bar.ShowsTerminal, bar.ShowsConfirm));
        Assert.Empty(_server.Challenges);

        await bar.CopyTerminal();

        Assert.Equal("sindri recipe approve workflow.step", _fixture.Clipboard.Text);
    }
}
