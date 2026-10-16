using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The subject the start names (spec section 3.11) and what the workflow.started row records of it.</summary>
internal sealed partial class RecipeWorkflow
{
    /// <summary>The producer of an input the start generated from its template.</summary>
    private const string GenerateActor = "engine:generate";

    /// <summary>
    /// The instance's subject (spec section 3.11): the first input names it, and the recipe's name, rendered once from the facts it
    /// reads at the start time, is its label. A recipe registered before input kinds is not named. Each fact is read by the 'ReadSubject' local activity; one that can't be read is its bare key.
    /// The stored text of the link inputs comes back too: a link fact's ref shows its URL, host and title from it.
    /// </summary>
    private static async Task<(SubjectRef? Subject, IReadOnlyDictionary<FactKey, string> Links)> NamedSubject(InterpretedRecipe recipe, InstanceStart start)
    {
        if (SubjectKinds.Legacy(recipe.Recipe) is not null)
        {
            return (null, new Dictionary<FactKey, string>());   // a pre-P4c instance replayed: no kind to name it by (the start refuses such a recipe now), so it runs unnamed
        }

        var inputs = recipe.Recipe.Inputs;
        List<FactKey> keys = [.. InstanceNames.FactsRead(recipe.Recipe)];
        if (SubjectRefs.NeedsContent(recipe))
        {
            keys.Add(inputs[0].Key);
        }

        keys.AddRange(inputs.Where(static i => SubjectKinds.Effective(i) == "link").Select(static i => i.Key));
        var contents = new Dictionary<FactKey, string?>();
        var links = new Dictionary<FactKey, string>();
        foreach (var key in keys.Distinct())
        {
            if (inputs.Any(i => i.Key == key && i.Kind is not ("item" or "file")) && start.Inputs.TryGetValue(key, out var fact))
            {
                var text = await SubjectTextOf(fact.Value).ConfigureAwait(true);
                contents[key] = text;
                if (text is not null && inputs.Any(i => i.Key == key && SubjectKinds.Effective(i) == "link"))
                {
                    links[key] = text;
                }
            }
        }

        var name = InstanceNames.Render(recipe, start.Item, contents, Now);
        return (SubjectRefs.Of(recipe, start.Item, contents.GetValueOrDefault(inputs[0].Key), name), links);
    }

    /// <summary>
    /// The workflow.started detail with what the ledger alone must show (spec, P4c amendment 'When name is rendered'): the subject with
    /// the rendered name as its label, and each fact's producer, 'engine:generate' for an input the start generated.
    /// </summary>
    private WorkflowEventDetail Named(WorkflowEventDetail detail, InstanceStart start) =>
        detail with
        {
            Delta = detail.Delta with
            {
                Subject = _subject is { } s ? new EventSubject(s.Kind, s.Key, s.Label) : null,
                Producers = detail.Delta.Facts?.Keys.ToDictionary(
                    static k => k,
                    k => start.Inputs.TryGetValue(FactKey.From(k), out var input) && input.Generated ? GenerateActor : detail.TriggerActor,
                    StringComparer.Ordinal),
            },
        };
}
