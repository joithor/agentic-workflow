using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Ship;

/// <summary>'step.ship.freeze' on the block runtime (line spec §7.5, SHIP-SHA-001): proof, review and hand-off must agree on the branch head; then the hand-off packet is machine-frozen.</summary>
/// <param name="diff">Reads the branch head.</param>
/// <param name="packets">Freezes the hand-off packet.</param>
internal sealed class ShipFreezeStep(IRunDiff diff, IPackets packets) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.ship.freeze");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        _ = (diff, packets);
        throw new NotSupportedException("step.ship.freeze is not built yet");
    }
}
