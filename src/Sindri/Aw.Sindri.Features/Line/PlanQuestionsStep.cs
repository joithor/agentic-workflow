using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Line;

/// <summary>'step.plan.questions', on the block runtime (line spec §6.1): turns the design's open decisions into the stop's question form; 'asked' or 'none'.</summary>
internal sealed class PlanQuestionsStep() : LineStep(Id), IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.plan.questions");

    /// <inheritdoc/>
    protected override IReadOnlyList<string> Structured => ["design", "answers", "answerLog"];

    /// <inheritdoc/>
    protected override Result<LineVerdict> Decide(LineFacts facts) => PlanQuestions.Build(facts["design"], facts["answerLog"]);
}
