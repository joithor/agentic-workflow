using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>'step.prove.intake' on the block runtime (line spec §7.3): loads the phase's items and signals and seeds the prove station's facts.</summary>
/// <param name="files">Reads the phase packet's files.</param>
internal sealed class ProveIntakeStep(IBriefFiles files) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.intake");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red: {files.GetType().Name}");
}
