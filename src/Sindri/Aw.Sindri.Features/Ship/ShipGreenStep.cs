using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Ship;

/// <summary>'step.ship.green' on the block runtime (line spec §7.5, SHIP-SECRET-001): the secret scan, then the profile's green commands under one heavy slot.</summary>
internal sealed class ShipGreenStep : IBlockStep, IBlockStepId
{
    public ShipGreenStep(IRunDiff diff, IBranchChanges changes, IPacketFiles files, RepoCommandRunner commands, ITextScrubber scrubber)
    {
        _ = (diff, changes, files, commands, scrubber);
    }

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.ship.green");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException("red");
}
