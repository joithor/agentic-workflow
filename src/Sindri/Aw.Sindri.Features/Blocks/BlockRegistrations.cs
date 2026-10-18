using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Features.Blocks.Evidence;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Features.Blocks.Step;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features.Blocks;

/// <summary>
/// The blocks and events this build ships (spec §12): pure blocks with AddGate/AddStep/…, the P6 effects with
/// AddEffect, the desk Steps as descriptors only (AddBlockDescriptor), and the events with AddEvent.
/// </summary>
internal static class BlockRegistrations
{
    /// <summary>Registers the shipped blocks and events.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddRecipeBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(DeskSteps.Produce)
        .AddBlockDescriptor(DeskSteps.Checks)
        .AddBlockDescriptor(DeskSteps.Verify)
        .AddBlockDescriptor(ChoiceSteps.JudgeChoose)
        .AddBlockDescriptor(ChoiceSteps.DeskChoose)
        .AddBlockDescriptor(BugfixSteps.IntakeTicket)
        .AddBlockDescriptor(BugfixSteps.RootcauseInvestigate)
        .AddBlockDescriptor(BugfixSteps.ReproduceUiCheck)
        .AddBlockDescriptor(BugfixSteps.ReproduceTestCheck)
        .AddBlockDescriptor(BugfixSteps.StartAttempt)
        .AddBlockDescriptor(BugfixSteps.RecordCandidate)
        .AddBlockDescriptor(BugfixSteps.Recheck)
        .AddBlockDescriptor(BugfixSteps.Judge)
        .AddBlockDescriptor(BugfixSteps.ReportWrite)
        .AddBlockDescriptor(ProofSteps.HandtestRun)
        .AddBlockDescriptor(ProofSteps.VerifyJourney)
        .AddBlockDescriptor(ProofSteps.VerifyDesign)
        .AddBlockDescriptor(EvidenceSteps.UiPlan)
        .AddBlockDescriptor(EvidenceSteps.ApiPlan)
        .AddBlockDescriptor(EvidenceSteps.DbPlan)
        .AddBlockDescriptor(EvidenceSteps.UiRun)
        .AddBlockDescriptor(EvidenceSteps.ApiRun)
        .AddBlockDescriptor(EvidenceSteps.DbRun)
        .AddBlockDescriptor(EvidenceSteps.None)
        .AddBlockDescriptor(EvidenceSteps.Combine)
        .AddBlockDescriptor(EvidenceSteps.Desk)
        .AddBlockDescriptor(ReviewSteps.Run)
        .AddBlockDescriptor(ReviewSteps.Fix)
        .AddBlockDescriptor(ReviewSteps.Recheck)
        .AddBlockDescriptor(OwnerSteps.Decide)
        .AddScopingBlocks()
        .AddStandInBlocks()      // TEMPORARY: drop with the stand-in recipes when engine's build, prove and ship land
        .AddBlockDescriptor(EvidenceProviders.Check)
        .AddGate<BudgetAvailableGate>()
        .AddGate<RouteStartableGate>()     // engine P7 unit U1: a stub over the profile's workflow.route
        .AddEffect<ClaimRefEffect>()      // engine P6 unit (d)
        .AddEffect<NotifyDeskEffect>()    // engine P6 unit (c)
        .AddEvent(RecipeEvents.ItemRouted)
        .AddEvent(RecipeEvents.ClaimCreated)
        .AddEvent(RecipeEvents.NotifyDelivered);

    /// <summary>TEMPORARY: registers the stand-in Steps of workflow.build, workflow.prove and workflow.ship until engine's recipes land.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddStandInBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(StandInBuildSteps.Build);

    /// <summary>Registers the scoping station's Steps and the context Step inputs it names (scoping spec §9), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddScopingBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(ScopingSteps.Intake)
        .AddBlockDescriptor(ScopingSteps.ReadBrief)
        .AddBlockDescriptor(ScopingSteps.ReadTracker)
        .AddBlockDescriptor(ScopingSteps.ReadChange)
        .AddBlockDescriptor(ScopingSteps.BriefSeed)
        .AddBlockDescriptor(ScopingSteps.BriefQuestions)
        .AddBlockDescriptor(ScopingSteps.BriefWrite)
        .AddBlockDescriptor(ScopingSteps.Anchor)
        .AddBlockDescriptor(ScopingSteps.Gather)
        .AddBlockDescriptor(ScopingSteps.Draft)
        .AddBlockDescriptor(ScopingSteps.Check)
        .AddBlockDescriptor(ScopingSteps.InterviewQuestions)
        .AddBlockDescriptor(ScopingSteps.InterviewAnswer)
        .AddBlockDescriptor(ScopingSteps.InterviewRecord)
        .AddBlockDescriptor(ScopingSteps.InterviewApply)
        .AddBlockDescriptor(ScopingSteps.Reanchor)
        .AddBlockDescriptor(ScopingSteps.Challenge)
        .AddBlockDescriptor(ScopingSteps.ChallengeLog)
        .AddBlockDescriptor(ScopingSteps.Propose)
        .AddBlockDescriptor(ScopingSteps.Evidence)
        .AddBlockDescriptor(ScopingSteps.Review)
        .AddBlockDescriptor(ScopingSteps.Freeze)
        .AddBlockDescriptor(ScopingSteps.Withdraw)
        .AddBlockDescriptor(PendingInputs.Lenses)
        .AddBlockDescriptor(PendingInputs.Code)
        .AddBlockDescriptor(PendingInputs.History);
}
