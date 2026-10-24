using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>'step.scope.gap-judge' on the block runtime: one judge call per candidate over <see cref="GapJudgeProjection"/>.</summary>
/// <param name="judge">The judge's choose question.</param>
/// <param name="scrubber">Redacts the text before it leaves for the judge.</param>
internal sealed class GapJudgeStep(IJudgeChoice judge, ITextScrubber scrubber) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.gap-judge");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(judge);
        ArgumentNullException.ThrowIfNull(scrubber);
        throw new NotSupportedException();
    }
}
