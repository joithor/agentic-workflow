using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Evidence;

/// <summary>'step.evidence.none' on the block runtime (decision-tree spec §3.3): nothing needs evidence beyond the always-run checks, and the summary says so.</summary>
internal sealed class EvidenceNoneStep : IBlockStep, IBlockStepId
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.evidence.none");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Read(context, "fact:built") is not { } built)
        {
            return ValueTask.FromResult(BlockStepResult.Failed("step.evidence.none needs fact:built, which the instance does not hold"));
        }

        var summary = new JsonObject
        {
            ["kind"] = "none",
            ["built"] = built.DeepClone(),
            ["text"] = "No evidence run was needed: the always-run checks are the proof for this change.",
        };
        return ValueTask.FromResult(StepFacts.Done("done", ("fact:evidenceSummary", summary)));
    }
}
