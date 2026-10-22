using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>'step.prove.collect' on the block runtime (BS-13): hashes the files the hand test, verify and evidence children name and appends them to fact:proof.</summary>
/// <param name="files">Hashes the named files.</param>
/// <param name="clock">Stamps each artifact.</param>
internal sealed class ProveCollectStep(IArtifactFiles files, TimeProvider clock) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.collect");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        _ = (files, clock);
        throw new NotSupportedException();
    }
}
