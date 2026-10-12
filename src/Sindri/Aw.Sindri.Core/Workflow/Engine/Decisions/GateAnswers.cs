using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// The answers of the matched transition's gates and evidence, evaluated by the gate activities before the decision
/// (spec §5.2). A gate or evidence check with no answer had no runtime: it refuses (fail closed, W5).
/// A command never evaluates a gate itself; its resume is decided as a system trigger with these answers.
/// </summary>
/// <param name="Gates">Each gate's answer, by gate.</param>
/// <param name="Evidence">Each evidence check's answer.</param>
public sealed record GateAnswers(IReadOnlyDictionary<BlockId, GateResult> Gates, IReadOnlyDictionary<EvidenceCheck, EvidenceResult> Evidence)
{
    /// <summary>No answers: for a trigger whose match has no gates or evidence.</summary>
    public static GateAnswers None { get; } = new(new Dictionary<BlockId, GateResult>(), new Dictionary<EvidenceCheck, EvidenceResult>());
}
