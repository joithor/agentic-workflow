using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Ship;

/// <summary>'step.ship.pack' on the block runtime (line spec §7.5): not built yet.</summary>
/// <param name="files">Reads the phase, proof and review packets and writes the hand-off draft.</param>
/// <param name="checkouts">Where the repo is checked out (a relative phase packet directory is read under it).</param>
/// <param name="holdouts">The hidden-scenario store, for the canary phrase.</param>
/// <param name="diff">Reads the phase worktree's head.</param>
/// <param name="scrubber">Redacts secrets from the title, labels and body.</param>
internal sealed class ShipPackStep(IPacketDirectory files, IRepoCheckouts checkouts, IHoldoutStore holdouts, IRunDiff diff, ITextScrubber scrubber) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.ship.pack");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
