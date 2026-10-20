using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Evidence;

/// <summary>'step.evidence.combine' on the block runtime (decision-tree spec §5.3): the UI, API and database summaries become one.</summary>
internal sealed class EvidenceCombineStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.evidence.combine");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Missing(context, "fact:evidenceUi", "fact:evidenceApi", "fact:evidenceDb") is { Count: > 0 } missing)
        {
            return ValueTask.FromResult(BlockStepResult.Failed($"step.evidence.combine needs {string.Join(", ", missing)}, which the instance does not hold"));
        }

        var summary = new JsonObject
        {
            ["kind"] = "combined",
            ["ui"] = StepFacts.Read(context, "fact:evidenceUi")?.DeepClone(),
            ["api"] = StepFacts.Read(context, "fact:evidenceApi")?.DeepClone(),
            ["db"] = StepFacts.Read(context, "fact:evidenceDb")?.DeepClone(),
        };
        return ValueTask.FromResult(StepFacts.Done("done", ("fact:evidenceSummary", summary)));
    }
}
