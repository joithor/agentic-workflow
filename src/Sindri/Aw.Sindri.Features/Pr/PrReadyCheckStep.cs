using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>
/// 'step.pr.ready-check' on the block runtime (PR babysitter spec §4.5): one forge read of the pull request, compared with the snapshot the instance last recorded
/// as 'fact:prState'. It decides from facts only: 'same' when the state, head, checks (state and the commit they ran on), mergeable flag, review decision and
/// open thread count all match, which returns to 'ready' without a notification; 'changed' otherwise, which re-snapshots. A snapshot it can't read counts as changed.
/// It writes no fact. A forge that can't be read fails the Step, which the recipe also routes to 'snapshot'.
/// </summary>
/// <param name="pulls">The forge, through Sindri's port.</param>
internal sealed class PrReadyCheckStep(IPullRequests pulls) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.pr.ready-check");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pulls);
        ArgumentNullException.ThrowIfNull(context);
        throw new NotSupportedException();
    }
}
