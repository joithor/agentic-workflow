using Aw.Context.Testing.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ApproveAllRecipes;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.Jobs;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApproveRecipe;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.ApproveAll.ApproveAllFixtures;

namespace Aw.Sindri.Tests.AppConfirm;

/// <summary>
/// Architecture test (a) of setup-once spec §4 item 7: each AppConfirm row's handler, driven with a refusing IChallenges, leaves the ledger, the jobs, the profile and the approvals
/// unchanged. The challenge is redeemed before the handler's first write of any kind.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RefuseBeforeWritingTests
{
    private static readonly ChallengeId _missing = ChallengeId.For(new Guid(0x99, 0, 0, new byte[8]));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ApproveProfile_writes_nothing_when_the_confirmation_is_refused()
    {
        var h = new SliceHarness();
        var refusing = new RefusingChallenges();
        var live = SliceHarness.Profile("5s");
        h.Profiles.Live = live;
        _ = await h.HoldLease();
        var service = new ApproveProfileService(h.Profiles, h.Profiles, h.Held, h.Store, h.Events, refusing, new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog()));
        var eventsBefore = h.Store.Events.Count;

        var approved = await service.Handle(new ApproveProfileRequest(live.Hash, new ChallengeAnswer(_missing, "approve profile " + live.Hash.Abbreviated)), Token);

        Assert.Equal("SND-PROFILE-004", approved.Error.Code);
        Assert.Equal(1, refusing.Redeemed);
        Assert.Equal(eventsBefore, h.Store.Events.Count);
        Assert.Null((await h.Profiles.Latest(Token)).Value);
    }

    [Fact]
    public async Task ApproveRecipe_writes_nothing_when_the_confirmation_is_refused()
    {
        var refusing = new RefusingChallenges();
        await using var registry = new RegistryHarness(services => services.AddSingleton<IChallenges>(refusing));
        _ = await registry.H.HoldLease();
        var step = await registry.Shipped("sindri/workflow.step.yaml");
        var human = A.Caller().Human().Client(ClientLabel.Ui).Build();
        var eventsBefore = registry.H.Store.Events.Count;
        using var entered = registry.H.Callers.Enter(human);
        await using var scope = registry.Services.CreateAsyncScope();

        var approved = await scope.ServiceProvider.GetRequiredService<ApproveRecipeService>()
            .Handle(new ApproveRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(_missing, "approve workflow.step 4")), Token);

        Assert.Equal("SND-RECIPE-009", approved.Error.Code);
        Assert.Equal(1, refusing.Redeemed);
        Assert.Equal(eventsBefore, registry.H.Store.Events.Count);
        Assert.Null((await registry.Approvals.Latest(step.Id, step.Version, step.Hash, Token)).Value);
        Assert.Empty(registry.Wake.Sent);
    }

    [Fact]
    public async Task ApproveAllRecipes_claims_no_job_and_keeps_nothing_when_the_confirmation_is_refused()
    {
        var h = new SliceHarness();
        var refusing = new RefusingChallenges();
        var shipped = new FakeShippedRecipes(StepAndItem());
        var jobs = new JobRegistry();
        var kept = new RedeemedPlans();
        _ = await h.Profiles.Save(SliceHarness.Profile(), Token);
        using var caller = h.Callers.EnterService("test");
        var admission = new ApproveAllRecipesAdmission(new Core.Workflow.ApproveAll.ApproveAllPlanReader(h.Profiles, shipped), refusing, jobs, kept, h.Callers);
        var request = new ApproveAllRecipesRequest(new ChallengeAnswer(_missing, "approve-all 000000"));
        var eventsBefore = h.Store.Events.Count;

        var admitted = await admission.Admit(request, Token);

        Assert.Equal("SND-RECIPE-009", admitted.Error.Code);
        Assert.Equal(1, refusing.Redeemed);
        Assert.False(jobs.IsRunning(ApproveAllRecipesAdmission.Kind, ApproveAllRecipesAdmission.Key, out _));
        Assert.False(kept.Take(request, out _));
        Assert.Equal(eventsBefore, h.Store.Events.Count);
        Assert.Empty(shipped.Approved);
    }
}
