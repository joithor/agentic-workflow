using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Build;

/// <summary>'step.build.amend' on the block runtime (line spec §7.2, LN-PKT5): the owner's grant becomes a new child phase packet, owner-frozen, and the packet in force.</summary>
/// <param name="packets">Freezes the child packet.</param>
/// <param name="files">Reads the parent packet and writes the child into the run area.</param>
/// <param name="area">Keeps the LC-7 escape line.</param>
/// <param name="clock">Stamps the amendment.</param>
internal sealed class BuildAmendStep(IPackets packets, IPacketFiles files, IRunArea area, TimeProvider clock) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.build.amend");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        _ = (packets, files, area, clock);
        throw new NotSupportedException("step.build.amend is not built yet");
    }
}
