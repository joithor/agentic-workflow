using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.BugFix;

/// <summary>'step.bugfix.record-candidate' on the block runtime (stub until the green commit).</summary>
/// <param name="state">bugfix-state.</param>
/// <param name="area">The instance's run area, where its bug fix state lives.</param>
internal sealed class RecordCandidateStep(IBugFixState state, IRunArea area) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.bugfix.record-candidate");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) 
    {
        _ = (state, area);
        throw new NotSupportedException();
    }
}
