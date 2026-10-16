using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// What a child's end carries to its parent (DT spec §8.4): its declared outputs are the facts of the parent's
/// 'wait.satisfied', and the ones present on an outcome are those recorded on every path into a terminal state that ends with it.
/// </summary>
internal static class ChildOutputs
{
    private static readonly OutcomeName _cancelled = OutcomeName.From("cancelled");

    /// <summary>The keys of <paramref name="child"/>'s declared outputs.</summary>
    public static IReadOnlyList<FactKey> Declared(Recipe child) => [.. child.Outputs.Select(o => o.Key).Distinct()];

    /// <summary>
    /// For each outcome the child can end with, the declared outputs present on every path to it (an output is never an
    /// input, RCP-OUT-003, so reaching and producing are the same). An outcome no reached terminal state ends with carries
    /// nothing, and so does 'cancelled', which the engine ends the child with before any output is recorded.
    /// </summary>
    public static IReadOnlyDictionary<OutcomeName, IReadOnlySet<FactKey>> PresentOn(Recipe child)
    {
        ArgumentNullException.ThrowIfNull(child);
        var reaching = FactsReaching.Of(child);
        var declared = Declared(child).ToHashSet();
        var present = new Dictionary<OutcomeName, IReadOnlySet<FactKey>> { [_cancelled] = new HashSet<FactKey>() };
        foreach (var outcome in child.States.Values.Where(s => s.Kind == StateKind.Terminal && s.Outcome is not null).Select(s => s.Outcome.GetValueOrDefault()).Distinct())
        {
            var reached = child.States
                .Where(s => s.Value.Kind == StateKind.Terminal && s.Value.Outcome == outcome && reaching.ContainsKey(s.Key))
                .Select(s => reaching[s.Key])
                .ToList();
            HashSet<FactKey> carried = reached.Count == 0 ? [] : [.. declared];
            foreach (var facts in reached)
            {
                carried.IntersectWith(facts);
            }

            present[outcome] = carried;
        }

        return present;
    }
}
