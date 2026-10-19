using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>'step.prove.evidence' on the block runtime (line spec §7.3): the proof rules, then the proof pack in the run area, machine-frozen.</summary>
/// <param name="files">Reads the phase packet's files.</param>
/// <param name="area">The instance's run area, where the pack is written.</param>
/// <param name="packets">Freezes the pack.</param>
/// <param name="checkouts">Where the repo is checked out (the packet's base commit is read from it).</param>
internal sealed class ProveEvidenceStep(IBriefFiles files, IRunArea area, IPackets packets, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.evidence");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red: {files.GetType().Name}{area.GetType().Name}{packets.GetType().Name}{checkouts.GetType().Name}");
}
