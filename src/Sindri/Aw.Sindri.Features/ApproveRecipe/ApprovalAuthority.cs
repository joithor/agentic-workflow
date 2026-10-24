using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.ApproveRecipe;

/// <summary>
/// What lets an approval through: a human redeems the challenge, and an agent (a session or a service) needs none, because its approval
/// holds when the recipe's evals pass (owner q93, 2026-10-23).
/// </summary>
/// <param name="challenges">The server's challenges.</param>
/// <param name="evalGate">Runs an agent's eval files.</param>
internal sealed class ApprovalAuthority(IChallenges challenges, AgentEvalGate evalGate)
{
    /// <summary>Checks <paramref name="request"/> against <paramref name="plan"/> for <paramref name="caller"/>.</summary>
    /// <param name="request">The approval asked for.</param>
    /// <param name="plan">The plan the approval is checked against.</param>
    /// <param name="caller">Who asks.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Nothing when it may go ahead; the challenge's or the evals' refusal.</returns>
    public async ValueTask<Result<Unit>> Authorize(ApproveRecipeRequest request, ApprovalPlan plan, CallerContext caller, CancellationToken cancellationToken)
    {
        if (caller.Class != CallerClass.Human)
        {
            return await evalGate.Prove(plan.Recipe, request.Evals ?? [], cancellationToken).ConfigureAwait(false);
        }

        if (request.Answer is not { } answer)
        {
            return SindriErrors.InvalidInput.With("a person approves with the challenge's typed text (fix: sindri recipe approve <recipe>)");
        }

        var redeemed = challenges.Redeem(answer, plan.Action, plan.Subject, WorkflowErrors.RecipeChallengeStale);
        return redeemed.IsError ? redeemed.Error : Unit.Value;
    }
}
