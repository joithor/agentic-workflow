using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>
/// 'step.scope.withdraw', on the block runtime (scoping spec §6.1, C9, Q14): resubmits the run with no surfaces, which withdraws its undecided map
/// proposals (the ones a person accepted, merged, renamed or moved stay). An item with no repo has none to withdraw. A context engine that cannot answer fails the Step,
/// since 'withdrawn' would say a drop left nothing behind when it left the proposals standing.
/// </summary>
/// <param name="proposals">The run's proposals on the context map.</param>
internal sealed class ScopeWithdrawStep(IScopeRunProposals proposals) : IBlockStep, IDescribedBlock
{
    private static readonly OutcomeName _withdrawn = OutcomeName.From("withdrawn");

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.Withdraw;

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.Repo is { } repo && context.Address is { } address)
        {
            var withdrawn = await proposals.Replace(address, repo, [], cancellationToken).ConfigureAwait(false);
            if (withdrawn.IsError)
            {
                return BlockStepResult.Failed($"the run's proposals were not withdrawn: {withdrawn.Error.Code} {withdrawn.Error.Message}");
            }
        }

        return BlockStepResult.Completed(_withdrawn, new Dictionary<FactKey, System.Text.Json.JsonElement>());
    }
}
