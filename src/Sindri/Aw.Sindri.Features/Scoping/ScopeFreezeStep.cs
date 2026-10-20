using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.scope.freeze' on the block runtime (scoping spec §3 'Freeze'). Not built yet.</summary>
/// <param name="area">The instance's run area, where the scope packet is kept.</param>
/// <param name="packets">Hashes and freezes the packet.</param>
internal sealed class ScopeFreezeStep(IRunArea area, IPackets packets) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.freeze");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{Id.Value} is not built yet ({area.GetType().Name}, {packets.GetType().Name})");
}
