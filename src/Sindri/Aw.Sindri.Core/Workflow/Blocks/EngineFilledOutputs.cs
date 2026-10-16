using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>What the engine writes for an output a report omits (DT spec §8.1): the content of an engine-filled fact.</summary>
public static class EngineFilledOutputs
{
    /// <summary>The engine-filled outputs of a report.</summary>
    /// <param name="outputs">The Step's declared outputs.</param>
    /// <param name="outcome">The reported outcome, written as the choice.</param>
    /// <param name="provider">"desk" for a human's report, "eval" for a recipe eval.</param>
    /// <param name="actor">Who reported; null in an eval, which has no actor.</param>
    /// <returns>Each engine-filled fact with its JSON content: {"choice","provider","actor"}.</returns>
    public static IReadOnlyDictionary<FactKey, string> For(IReadOnlyList<OutputSpec> outputs, OutcomeName outcome, string provider, string? actor) =>
        throw new NotSupportedException("EngineFilledOutputs.For is not implemented yet");
}
