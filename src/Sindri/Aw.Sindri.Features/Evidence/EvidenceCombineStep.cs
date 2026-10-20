using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Evidence;

/// <summary>'step.evidence.combine' on the block runtime (decision-tree spec §5.3): the UI, API and database summaries become one. A part whose summary says 'selected: false' (its plan found no target) is left out; the Step fails when every part is, so the phase routes to evidence.none.</summary>
internal sealed class EvidenceCombineStep : IBlockStep, IBlockStepId
{
    private static readonly (string Part, string Key)[] _parts = [("ui", "fact:evidenceUi"), ("api", "fact:evidenceApi"), ("db", "fact:evidenceDb")];

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

        var summary = new JsonObject { ["kind"] = "combined" };
        foreach (var (part, key) in _parts)
        {
            var node = StepFacts.Read(context, key);
            switch (Selected(node))
            {
                case null:
                    return ValueTask.FromResult(BlockStepResult.Failed($"step.evidence.combine: {key} has a 'selected' that is not true or false"));
                case true:
                    summary[part] = node!.DeepClone();   // why: a summary that selects is not null
                    break;
                default:
                    break;
            }
        }

        if (summary.Count == 1)
        {
            return ValueTask.FromResult(BlockStepResult.Failed("no evidence target in this phase; route it to evidence.none"));
        }

        return ValueTask.FromResult(StepFacts.Done("done", ("fact:evidenceSummary", summary)));
    }

    // Whether a part's summary stands: true unless it says 'selected: false'; null when its 'selected' is anything but a boolean.
    private static bool? Selected(JsonNode? summary)
    {
        if (summary is not JsonObject { } marked || !marked.TryGetPropertyValue("selected", out var flag))
        {
            return summary is not null;
        }

        return flag is JsonValue value && value.TryGetValue<bool>(out var selected) ? selected : null;
    }
}
