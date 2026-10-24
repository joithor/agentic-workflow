using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping.Relevance;

/// <summary>'step.scope.relevance-judge' on the block runtime (spec 4.2).</summary>
/// <param name="judge">The judge's choose question.</param>
/// <param name="scrubber">Redacts the projection's text before it leaves for the judge.</param>
internal sealed class RelevanceJudgeStep(IJudgeChoice judge, ITextScrubber scrubber) : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.relevance-judge");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        _ = (judge, scrubber);
        throw new NotSupportedException();
    }
}
