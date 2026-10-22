using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Which answer fits which suspension (spec §3.8, §5.2, §5.8, §6.4, W7): each row's answer variants, the fix that
/// names them, and the bounds on a grant. The interpreter's Update validator calls it before accepting an answer; an
/// answer that fits runs the suspension's resume action, and '--reject' runs the refusal path instead. A suspension
/// mirroring a child forwards every answer to the child, which checks the fit (§5.8). The recorder reads the same table
/// to name an answer's variant (§9.2), so the two can't drift.
/// </summary>
internal static class AnswerFit
{
    /// <summary>
    /// Whether <paramref name="answer"/> fits the instance's suspension: '--grant' on rounds or no-progress,
    /// '--grant-tokens' on budget, '--text' on needs-answer (from a Step) or no-progress, '--resume [--text]' on a wait's
    /// needs-answer, a timeout, SND-WF-010 or a dispatch refusal, '--accept' or '--reject' on a gate escalation.
    /// '--grant n' is within 1 to the loop's cap and '--grant-tokens n' at most the original budget when the recipe is given.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <param name="recipe">Its recipe, for the bounds; null skips the upper bounds.</param>
    /// <param name="answer">The answer.</param>
    /// <returns>The resume action, or SND-WF-003 whose fix names the answer that fits.</returns>
    public static Result<ResumeAction> Check(WorkflowInstance instance, Recipe? recipe, AnswerGiven answer)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(answer);
        var address = instance.Address.Value;
        if (Unanswerable(instance, address) is { } refused)
        {
            return refused;
        }

        var suspension = instance.Suspension!; // why: Unanswerable refuses an instance with no suspension
        if (suspension.Mirrors is not null)
        {
            return suspension.Resume;
        }

        var row = FitRow.For(suspension, address, instance.Recipe);
        if (!AnswerVariants.Fitting(suspension).Contains(answer.Variant))
        {
            return FitRefusals.Refuse($"{Spelled(answer.Variant)} doesn't fit {address}, {row.Shown}", row.Fix);
        }

        if (OutOfBounds(instance, suspension, recipe, answer, address) is { } bounded)
        {
            return bounded;
        }

        return answer.Variant == AnswerVariant.Reject ? suspension.Resume with { Kind = ResumeKind.Refuse } : suspension.Resume;
    }

    private static Error? Unanswerable(WorkflowInstance instance, string address)
    {
        if (instance.Ended)
        {
            return FitRefusals.Refuse($"{address} has ended", FitRefusals.Status(address));
        }

        if (instance.State == EngineStates.TakenOver)
        {
            return FitRefusals.Refuse($"{address} is taken over; no answer fits until it is handed back", $"sindri workflow handback {address}");
        }

        return instance.Suspension is null
            ? FitRefusals.Refuse($"{address} is in {instance.State}; nothing there waits for an answer", FitRefusals.Status(address))
            : null;
    }

    // W7: '--grant n' is 1 to the loop's cap; '--grant-tokens n' is 1 to the original budget (a per-round instance: 4x it). Once per park holds because the grant ends the park.
    private static Error? OutOfBounds(WorkflowInstance instance, Suspension suspension, Recipe? recipe, AnswerGiven answer, string address)
    {
        if (answer.Variant is not (AnswerVariant.Grant or AnswerVariant.GrantTokens))
        {
            return null;
        }

        var rounds = answer.Variant == AnswerVariant.Grant;
        var upper = recipe is null ? int.MaxValue : rounds ? LoopCap(instance.Frame, suspension, recipe) : Budget(instance, recipe);
        if (answer.Amount is { } n && n >= 1 && n <= upper)
        {
            return null;
        }

        var limit = upper == int.MaxValue ? "a whole number from 1" : "from 1 to " + upper.ToString(CultureInfo.InvariantCulture);
        return FitRefusals.Refuse(
            $"{Spelled(answer.Variant)} needs {(rounds ? "rounds" : "tokens")} {limit}",
            FitRefusals.Answer(address, rounds ? "--grant 1" : "--grant-tokens <n>"));
    }

    // The loop a grant raises (the same rule LoopRules.Grant applies): for kind loop, the looped state the withheld transition goes back into; for kind refusal, the active state's own.
    private static int LoopCap(Frame frame, Suspension suspension, Recipe recipe)
    {
        var looped = LoopRules.LoopedState(recipe, frame, suspension);
        return recipe.States.TryGetValue(looped, out var state) && state.Loop is { } name && recipe.Loops.TryGetValue(name, out var loop)
            ? loop.Cap
            : int.MaxValue;
    }

    // The original budget: the parked state's own, else the recipe's; under the per-round rule one grant may add up to 4x it (BudgetRules.GrantTokens).
    private static int Budget(WorkflowInstance instance, Recipe recipe)
    {
        var own = recipe.States.TryGetValue(instance.Frame.State, out var state) ? state.Budget?.Tokens : null;
        var original = own ?? recipe.Budget?.Tokens;
        return (instance.Spent.IsPerRound ? BudgetRules.GrantBound(original) : original) ?? int.MaxValue;
    }

    private static string Spelled(AnswerVariant variant) => variant switch
    {
        AnswerVariant.Text => "--text",
        AnswerVariant.Grant => "--grant",
        AnswerVariant.GrantTokens => "--grant-tokens",
        AnswerVariant.Accept => "--accept",
        AnswerVariant.Reject => "--reject",
        AnswerVariant.Resume or _ => "--resume",
    };
}
