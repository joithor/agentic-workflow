using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>'step.prove.holdout-log' on the block runtime (line spec §7.3, §9.3).</summary>
/// <param name="store">The hidden-scenario store.</param>
/// <param name="commits">The phase's commits as text.</param>
/// <param name="files">Reads the phase packet's files.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
internal sealed class ProveHoldoutLogStep(IHoldoutStore store, ICommitText commits, IBriefFiles files, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.holdout-log");

    private readonly object[] _ports = [store, commits, files, checkouts];

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException($"holdout-log is not written yet ({_ports.Length} ports)");
}
