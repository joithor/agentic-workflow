using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>'step.phase.drift' on the block runtime (line spec §7.1): 'same' when review left the phase worktree's head at the proven one, 'moved' with the new built head and change when it did not.</summary>
/// <param name="diff">Reads the worktree's HEAD.</param>
internal sealed class PhaseDriftStep(IRunDiff diff) : IBlockStep, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PhaseSteps.Drift;

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) { _ = diff; throw new NotSupportedException(); }
}
