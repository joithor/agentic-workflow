using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Setup;
using static Aw.Sindri.Ui.Tests.Setup.SetupFixture;

namespace Aw.Sindri.Ui.Tests.Setup;

/// <summary>The two confirmations in their rows (setup spec §8.3): the profile's typed approval and the workflows' plan with its hold and typed alternative, each behind catalog readiness with the terminal copy until then.</summary>
[Lane(TestLane.Unit)]
public sealed class SetupConfirmTests
{
    private const string PhraseText = "approve profile 70e158";
    private const string LiveHash = "70e1581b3ecaf617318482f6b0844b67f79a287a7136b4e4b6ff5d26005e4261";

    private readonly SetupFixture _f;

    public SetupConfirmTests() : this("extra-approve-all-cli-only")
    {
    }

    private SetupConfirmTests(string catalog)
    {
        _f = new SetupFixture(catalog) { Hash = LiveHash };
        var challenge = JsonNode.Parse(GoldenFiles.Api("workflow-challenges-app", "profile"))!;   // why: the golden is a JSON document
        challenge["expiresAt"] = _f.Clock.GetUtcNow().AddMinutes(5).ToString("O", System.Globalization.CultureInfo.InvariantCulture);   // valid for five minutes from the fixture's clock
        _f.Server.AppChallengeAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, challenge.ToJsonString());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SetupStepViewModel Step(SetupViewModel setup, string id) => setup.Steps.Single(s => s.Id == id);

    private async Task<SetupStepViewModel> ProfileRow(SetupConfirmTests? ready = null)
    {
        var f = ready is null ? _f : ready._f;
        f.Serve("profile-needs-approval");
        return Step(await f.Read(Token), "profile");
    }

    [Fact]
    public async Task Profile_row_shows_terminal_copy_until_the_catalog_is_ready()
    {
        var waiting = await ProfileRow();

        Assert.Equal((true, false, false), (waiting.Action!.HasProtected, waiting.Profile!.ShowsTyped, waiting.Action.IsInApp));
        Assert.Empty(_f.Server.Challenges);   // nothing is asked for while the app may not approve
        Assert.Empty(_f.Server.ProfileApprovals);

        var ready = new SetupConfirmTests("extra-setup-ready");
        var row = await ProfileRow(ready);

        Assert.Null(row.Action);   // the typed box takes the button's place
        Assert.True(row.Profile!.ShowsTyped);
        Assert.Equal(["approve profile " + LiveHash[..6]], ready._f.Server.Challenges.Select(c => c.Action), StringComparer.Ordinal);
        Assert.Equal(PhraseText, row.Profile.TypedBox.Phrase);
    }

    [Fact]
    public async Task The_typed_phrase_sends_the_live_hash_once_and_approves_the_row()
    {
        var ready = new SetupConfirmTests("extra-setup-ready");
        var profile = (await ProfileRow(ready)).Profile!;
        profile.TypedBox.Text = PhraseText;

        await profile.SubmitTyped(profile.TypedBox.Submit()!);
        await profile.SubmitTyped(PhraseText);   // a second press after the approval sends nothing

        var sent = Assert.Single(ready._f.Server.ProfileApprovals);
        Assert.Equal((LiveHash, "CHL-00000000000000000000000000000006", PhraseText), (sent.Hash, sent.Answer.Challenge, sent.Answer.Typed));
        Assert.True(profile.IsDone);
    }

    [Fact]
    public async Task A_phrase_that_is_not_exact_sends_nothing()
    {
        var ready = new SetupConfirmTests("extra-setup-ready");
        var profile = (await ProfileRow(ready)).Profile!;

        await profile.SubmitTyped("approve profile 70e15");

        Assert.Empty(ready._f.Server.ProfileApprovals);
    }

    [Fact]
    public async Task A_stale_hash_says_to_read_it_again_and_asks_for_a_fresh_phrase()
    {
        var ready = new SetupConfirmTests("extra-setup-ready");
        ready._f.Server.ApproveProfileAnswer = RouteAnswer.Problem("profile-approve", "stale-hash");
        var profile = (await ProfileRow(ready)).Profile!;

        await profile.SubmitTyped(PhraseText);

        Assert.Equal((Labels.SetupProfileChangedWhileReading, false, 2), (profile.Refusal, profile.IsDone, ready._f.Server.Challenges.Count));   // one phrase for the first ask, one after the refusal
        Assert.True(profile.ShowsTyped);
    }

    [Fact]
    public async Task Without_a_profile_hash_the_row_says_it_is_reading_and_asks_for_nothing()
    {
        var ready = new SetupConfirmTests("extra-setup-ready");
        ready._f.Hash = null;

        var profile = (await ProfileRow(ready)).Profile!;

        Assert.Equal((false, true), (profile.ShowsTyped, profile.IsWaiting));
        Assert.Empty(ready._f.Server.Challenges);
    }

    [Fact]
    public async Task Workflows_row_keeps_the_terminal_copy_and_reads_no_plan_until_the_catalog_is_ready()
    {
        _f.Serve(With("profile-needs-approval", "recipes", "needs-you", "ApproveAllRecipes"));

        var row = Step(await _f.Read(Token), "recipes");

        Assert.Equal((true, 0), (row.Action!.HasProtected, _f.Server.PlanCalls));
        Assert.False(row.Workflows!.IsOpen);
    }

    [Fact]
    public async Task Workflows_row_shows_the_plan_with_its_change_words_and_no_button_when_ready()
    {
        var ready = new SetupConfirmTests("extra-setup-ready");
        ready._f.Serve(With("profile-needs-approval", "recipes", "needs-you", "ApproveAllRecipes"));

        var row = Step(await ready._f.Read(Token), "recipes");
        var all = row.Workflows!;

        Assert.Null(row.Action);
        Assert.Equal((true, true), (all.IsOpen, all.ShowsPlan));
        Assert.NotEmpty(all.Rows);
        Assert.All(all.Rows, r => Assert.NotEmpty(r.Reason));   // New, Updated or Needs a second look, as the plan serves it
        Assert.Empty(ready._f.Server.Approvals);   // reading the plan approves nothing
    }

    [Fact]
    public async Task A_workflows_row_that_stops_needing_the_person_closes_its_plan()
    {
        var ready = new SetupConfirmTests("extra-setup-ready");
        ready._f.Serve(With("profile-needs-approval", "recipes", "needs-you", "ApproveAllRecipes"));
        var setup = await ready._f.Read(Token);

        ready._f.Serve(With("profile-needs-approval", "recipes", "todo"));
        await setup.Refresh(Url, Token);

        Assert.False(Step(setup, "recipes").Workflows!.IsOpen);
    }

    [Fact]
    public async Task The_profile_row_reads_the_summary_once_per_hash_and_shows_it_even_before_the_app_may_approve()
    {
        _f.Server.ProfileAnswer = RouteAnswer.Api("profile", "repo-added");

        var setup = await _f.Read(Token);
        await setup.Refresh(Url, Token);

        var profile = Step(setup, "profile").Profile!;
        Assert.False(profile.ShowsTyped);   // the catalog is not ready here: terminal copy
        Assert.Equal(["demo"], profile.Summary.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(1, _f.Server.ProfileCalls);
    }

    [Fact]
    public async Task A_profile_row_that_stops_needing_the_person_drops_its_summary()
    {
        _f.Server.ProfileAnswer = RouteAnswer.Api("profile", "repo-added");
        var setup = await _f.Read(Token);
        _f.Serve(With("profile-needs-approval", "profile", "done"));

        await setup.Refresh(Url, Token);

        Assert.False(Step(setup, "profile").Profile!.Summary.HasSummary);
    }

    [Fact]
    public async Task A_summary_that_cannot_be_read_leaves_the_row_working_and_is_asked_for_again()
    {
        _f.Server.ProfileAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-API-001", "The control plane is down");
        var setup = await _f.Read(Token);

        _f.Server.ProfileAnswer = RouteAnswer.Api("profile", "changed");
        await setup.Refresh(Url, Token);

        Assert.Equal(["Services"], Step(setup, "profile").Profile!.Summary.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(2, _f.Server.ProfileCalls);
    }
}
