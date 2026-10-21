using Aw.Kernel;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// A plan whose approve-all challenge was redeemed (setup-once spec §5.3): the only thing the shipped-recipe approval takes, so a recipe is never approved without the
/// owner's one hold. It carries what each 'recipe.approved' records (the batch and the hold time), so the engine needs no ambient state. It has no public constructor;
/// <see cref="Redeem"/> is the only way to get one.
/// </summary>
public sealed class RedeemedPlan
{
    private RedeemedPlan(ShippedRecipePlan plan, IssuedChallenge challenge, CommandId batch, long? heldMs)
    {
        Plan = plan;
        Challenge = challenge;
        Batch = batch;
        HeldMs = heldMs;
    }

    /// <summary>The plan the challenge covers.</summary>
    public ShippedRecipePlan Plan { get; }

    /// <summary>The redeemed challenge.</summary>
    public IssuedChallenge Challenge { get; }

    /// <summary>The command id of the start: the 'batch' of every 'recipe.approved' this hold writes.</summary>
    [System.Text.Json.Serialization.JsonRequired]
    public CommandId Batch { get; }

    /// <summary>How long the owner held the confirmation for, when it was held; null when it was typed.</summary>
    public long? HeldMs { get; }

    /// <summary>Redeems <paramref name="answer"/> for 'approve-all &lt;digest&gt;' on the plan as it is now.</summary>
    /// <param name="challenges">The server's challenges.</param>
    /// <param name="answer">The challenge and the typed text.</param>
    /// <param name="plan">The plan as it is now.</param>
    /// <param name="batch">The command id of the start.</param>
    /// <param name="heldMs">The hold time, or null.</param>
    /// <returns>The redeemed plan; or SND-RECIPE-009 when the challenge is unknown, used, expired, typed differently or the plan changed since it was issued.</returns>
    public static Result<RedeemedPlan> Redeem(IChallenges challenges, ChallengeAnswer answer, ShippedRecipePlan plan, CommandId batch, long? heldMs = null)
    {
        ArgumentNullException.ThrowIfNull(challenges);
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(plan);
        var redeemed = challenges.Redeem(answer, ChallengeAction.ApproveAll(plan.Digest), ChallengeSubject.ForApproveAll(plan.Digest), WorkflowErrors.RecipeChallengeStale);
        return redeemed.IsError ? redeemed.Error : new RedeemedPlan(plan, redeemed.Value, batch, heldMs);
    }
}
