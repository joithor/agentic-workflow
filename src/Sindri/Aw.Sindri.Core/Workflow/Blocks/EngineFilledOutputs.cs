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
    /// <returns>Each engine-filled fact with its content.</returns>
    public static IReadOnlyDictionary<FactKey, DecisionFact> For(IReadOnlyList<OutputSpec> outputs, OutcomeName outcome, string provider, string? actor)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        ArgumentNullException.ThrowIfNull(provider);
        return outputs.Where(static o => o.EngineFilled == true).ToDictionary(static o => o.Fact, _ => new DecisionFact(outcome.Value, provider, actor));
    }

    /// <summary>The refusal for a report or eval line that writes an engine-filled output itself (the one text the desk and the eval give).</summary>
    /// <param name="steps">The Steps the report may be for: the Step it reports on, or every Step of the build when it isn't known.</param>
    /// <param name="written">The output keys the report gives.</param>
    /// <returns>"&lt;fact&gt; is written by the engine on '&lt;step&gt;' (fix: drop --output &lt;fact&gt;)", naming the first such key; null when none is engine-filled.</returns>
    public static string? Refusal(IEnumerable<BlockDescriptor> steps, IEnumerable<FactKey> written)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(written);
        var descriptors = steps.ToList();
        foreach (var fact in written)
        {
            var on = descriptors.Where(d => d.Outputs.Any(o => o.EngineFilled == true && o.Fact == fact)).Select(static d => d.Id.Value).Order(StringComparer.Ordinal).ToList();
            if (on.Count > 0)
            {
                return $"{fact.Value} is written by the engine on {string.Join(" and ", on.Select(static id => $"'{id}'"))} (fix: drop --output {fact.Value})";
            }
        }

        return null;
    }
}
