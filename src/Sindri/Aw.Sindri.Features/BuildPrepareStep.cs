using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>
/// 'step.build.prepare' on the block runtime (line spec §7.2): the phase worktree must be clean at the head it stands on, and the Step seeds
/// the build's facts (the packet in force is the phase packet; no red tests, no built head, no findings or answers yet). A dirty or missing
/// worktree fails the Step with the reason, so the instance parks instead of building on someone's half-done work.
/// </summary>
/// <param name="diff">Reads the worktree's HEAD and whether it holds changes.</param>
internal sealed class BuildPrepareStep(IRunDiff diff) : IBlockStep, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => BuildSteps.Prepare;

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) { _ = diff; throw new NotSupportedException(); }
}
