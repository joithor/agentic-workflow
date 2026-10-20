using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using static Aw.Sindri.Tests.ApproveAll.ApproveAllFixtures;

namespace Aw.Sindri.Tests.ApproveAll;

/// <summary>
/// What an approve-all start checks inside the gate, in the spec's order (setup-once spec §5.3): the profile, a running approve-all, then the challenge, redeemed last. A refusal
/// that is not the owner's must leave the hold unburned; nothing is written by the checks.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class ApproveAllRecipesAdmissionTests : IDisposable
{
    private readonly SliceHarness _slice = new();
    private readonly FakeShippedRecipes _shipped = new(StepAndItem());
    private readonly JobRegistry _jobs = new();
    private readonly RedeemedPlans _kept = new();

    private IDisposable? _caller;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _caller?.Dispose();

    private ApproveAllRecipesAdmission Admission()
    {
        _caller ??= _slice.Callers.EnterService("test");
        return new(new ApproveAllPlanReader(_slice.Profiles, _shipped), _slice.Challenges, _jobs, _kept, _slice.Callers);
    }

    private async Task Approved() => _ = await _slice.Profiles.Save(SliceHarness.Profile(), Token);

    private async Task<ChallengeAnswer> Hold()
    {
        var digest = (await _shipped.Plan(Token)).Value.Digest;
        var issued = _slice.Challenges.Issue(ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest));
        return new ChallengeAnswer(issued.Id, issued.Action.Text);
    }

    private bool Redeems(ChallengeAnswer answer, string digest) =>
        _slice.Challenges.Redeem(answer, ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest), Core.Workflow.WorkflowErrors.RecipeChallengeStale).IsSuccess;

    [Fact]
    public async Task Before_a_profile_is_approved_the_start_is_refused_and_the_hold_is_not_burned()
    {
        var answer = await Hold();

        var admitted = await Admission().Admit(new ApproveAllRecipesRequest(answer), Token);

        Assert.Equal("SND-SETUP-007", admitted.Error.Code);
        Assert.True(Redeems(answer, (await _shipped.Plan(Token)).Value.Digest));
    }

    [Fact]
    public async Task A_running_approve_all_is_a_conflict_that_names_it_and_does_not_burn_the_hold()
    {
        await Approved();
        var answer = await Hold();
        var running = JobId.From("JOB-" + new string('a', 32));
        Assert.True(_jobs.TryClaim(ApproveAllRecipesAdmission.Kind, ApproveAllRecipesAdmission.Key, running, out _));

        var admitted = await Admission().Admit(new ApproveAllRecipesRequest(answer), Token);

        Assert.Equal("SND-JOB-409", admitted.Error.Code);
        Assert.Equal(running.Value, admitted.Error.Extensions!.Single(static e => e.Key == "runningJob").Value.ToString()!.Trim('"'));
        Assert.True(Redeems(answer, (await _shipped.Plan(Token)).Value.Digest));
    }

    [Fact]
    public async Task A_held_answer_keeps_its_hold_time_for_recipe_approved()
    {
        await Approved();
        var digest = (await _shipped.Plan(Token)).Value.Digest;
        var issued = _slice.Challenges.Issue(ChallengeAction.ApproveAll(digest), ChallengeSubject.ForApproveAll(digest), ChallengeMode.Held, ChallengeChannel.App);
        _slice.Clock.Advance(IssuedChallenge.EarliestAnswer);
        var request = new ApproveAllRecipesRequest(new ChallengeAnswer(issued.Id, HeldMs: 1700));

        var admitted = await Admission().Admit(request, Token);

        Assert.True(admitted.IsSuccess, admitted.IsError ? admitted.Error.ToString() : null);
        Assert.True(_kept.Take(request, out var plan));
        Assert.Equal(1700, plan!.HeldMs);
    }

    [Fact]
    public async Task Another_text_than_the_challenge_is_refused_and_nothing_is_kept()
    {
        await Approved();
        var answer = await Hold() with { Typed = "approve-all 000000" };
        var request = new ApproveAllRecipesRequest(answer);

        var admitted = await Admission().Admit(request, Token);

        Assert.Equal("SND-RECIPE-009", admitted.Error.Code);
        Assert.False(_kept.Take(request, out _));
    }

    [Fact]
    public async Task A_plan_that_moved_since_the_preview_is_refused()
    {
        await Approved();
        var answer = await Hold();
        _shipped.Move(Item, Hash("ffffffffffff"));

        var admitted = await Admission().Admit(new ApproveAllRecipesRequest(answer), Token);

        Assert.Equal("SND-RECIPE-009", admitted.Error.Code);
    }

    [Fact]
    public async Task The_held_challenge_keeps_the_redeemed_plan_for_the_job_and_writes_nothing()
    {
        await Approved();
        var request = new ApproveAllRecipesRequest(await Hold());
        var ledgerBefore = _slice.Store.Events.Count;

        var admitted = await Admission().Admit(request, Token);

        Assert.True(admitted.IsSuccess);
        Assert.True(_kept.Take(request, out var plan));
        Assert.Equal([Step, Item], plan!.Plan.Recipes.Select(static r => r.Id));
        Assert.Equal(_slice.Callers.Current!.CommandId, plan.Batch);   // why: the admission ran under the caller entered above
        Assert.Equal(ledgerBefore, _slice.Store.Events.Count);
        Assert.Equal(0, _shipped.Applied);
    }

    [Fact]
    public async Task A_rolled_back_start_gives_the_plan_up()
    {
        await Approved();
        var admission = Admission();
        var request = new ApproveAllRecipesRequest(await Hold());
        _ = await admission.Admit(request, Token);

        admission.Rollback(request);

        Assert.False(_kept.Take(request, out _));
    }
}
