using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>
/// A plan an approve-all start took (setup-once spec §5.3): the only thing the shipped-recipe approval takes. It carries what each 'recipe.approved' records (the batch),
/// so the engine needs no ambient state. It has no public constructor; <see cref="For"/> is the only way to get one. No typed challenge stands behind it (owner decision q93);
/// the approval runs the same checks as ApproveRecipe.
/// </summary>
public sealed class RedeemedPlan
{
    private RedeemedPlan(ShippedRecipePlan plan, CommandId batch)
    {
        Plan = plan;
        Batch = batch;
    }

    /// <summary>The plan the start read.</summary>
    public ShippedRecipePlan Plan { get; }

    /// <summary>The command id of the start: the 'batch' of every 'recipe.approved' this start writes.</summary>
    [System.Text.Json.Serialization.JsonRequired]
    public CommandId Batch { get; }

    /// <summary>The plan as it is now, taken for the start whose command id is <paramref name="batch"/>.</summary>
    /// <param name="plan">The plan as it is now.</param>
    /// <param name="batch">The command id of the start.</param>
    /// <returns>The plan to approve.</returns>
    public static RedeemedPlan For(ShippedRecipePlan plan, CommandId batch)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new RedeemedPlan(plan, batch);
    }
}
