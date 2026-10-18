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
        .AddReviewBlocks()
        .AddBlockDescriptor(OwnerSteps.Decide)
        .AddScopingBlocks()
        .AddShipDocsBlocks()
        .AddBuildProveBlocks()
        .AddBlockDescriptor(EvidenceProviders.Check)
        .AddGate<BudgetAvailableGate>()
        .AddGate<RouteStartableGate>()     // engine P7 unit U1: a stub over the profile's workflow.route
        .AddEffect<ClaimRefEffect>()      // engine P6 unit (d)
        .AddEffect<NotifyDeskEffect>()    // engine P6 unit (c)
        .AddEvent(RecipeEvents.ItemRouted)
        .AddEvent(RecipeEvents.ClaimCreated)
        .AddEvent(RecipeEvents.NotifyDelivered);

    /// <summary>Registers the line's build and prove stations' Steps (line spec §7.2, §7.3), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddBuildProveBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(BuildSteps.Prepare)
        .AddBlockDescriptor(BuildSteps.Red)
        .AddBlockDescriptor(BuildSteps.RedCheck)
        .AddBlockDescriptor(BuildSteps.Green)
        .AddBlockDescriptor(BuildSteps.Checks)
        .AddBlockDescriptor(BuildSteps.ScopeDesk)
        .AddBlockDescriptor(BuildSteps.Amend)
        .AddBlockDescriptor(ProveSteps.Intake)
        .AddBlockDescriptor(ProveSteps.BeforeAfter)
        .AddBlockDescriptor(ProveSteps.Holdout)
        .AddBlockDescriptor(ProveSteps.HoldoutLog)
        .AddBlockDescriptor(ProveSteps.Evidence)
        .AddBlockDescriptor(ProveSteps.FailLog);

    /// <summary>Registers the review loop's Steps and the lens frame it shares (line spec DTC4), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddReviewBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(ReviewSteps.Round)
        .AddBlockDescriptor(LensSteps.Run)
        .AddBlockDescriptor(ReviewSteps.Log)
        .AddBlockDescriptor(ReviewSteps.Record)
        .AddBlockDescriptor(ReviewSteps.Fix)
        .AddBlockDescriptor(ReviewSteps.Recheck);

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

    /// <summary>Registers the line stations' Steps (ship, docs sync), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddShipDocsBlocks(this IServiceCollection services) => services.AddShipBlocks().AddDocsBlocks();

    private static IServiceCollection AddShipBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(ShipSteps.Green)
        .AddBlockDescriptor(ShipSteps.Pack)
        .AddBlockDescriptor(ShipSteps.Freeze)
        .AddBlockDescriptor(ShipSteps.Open)
        .AddBlockDescriptor(ShipSteps.Return);

    /// <summary>Registers the docs-sync station's Steps (PB spec §7), descriptors only.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    private static IServiceCollection AddDocsBlocks(this IServiceCollection services) => services
        .AddBlockDescriptor(DocsSteps.Scope)
        .AddBlockDescriptor(DocsSteps.Update)
        .AddBlockDescriptor(DocsSteps.Check)
        .AddBlockDescriptor(DocsSteps.OpenPr);
}
