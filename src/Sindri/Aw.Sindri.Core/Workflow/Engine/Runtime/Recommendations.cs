using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Reads 'fact:recommendation' ('{ "outcome": …, "why": … }'), which the step before a stop writes. Pure.</summary>
public static class Recommendations
{
    /// <summary>The recommendation <paramref name="text"/> holds when its outcome is one of <paramref name="outcomes"/>.</summary>
    /// <param name="text">The fact's stored text; null when there is none.</param>
    /// <param name="outcomes">The outcomes of the desk step the recommendation is for.</param>
    /// <returns>The recommendation; null when there is no text, it isn't a JSON object with a text 'outcome', or the outcome isn't one of <paramref name="outcomes"/>.</returns>
    public static DeskRecommendation? Of(string? text, IReadOnlyList<OutcomeName> outcomes)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(text);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("outcome", out var outcome) || outcome.ValueKind != JsonValueKind.String
                || outcome.GetString() is not { } name || !outcomes.Any(o => string.Equals(o.Value, name, StringComparison.Ordinal)))
            {
                return null;
            }

            var why = root.TryGetProperty("why", out var reason) && reason.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(reason.GetString()) ? reason.GetString() : null;
            return new DeskRecommendation(name, why);
        }
        catch (JsonException)
        {
            return null;   // boundary: the fact is text a step wrote, and a bad one is dropped, not an error
        }
    }

    /// <summary>Whether <paramref name="text"/> is a recommendation the state <paramref name="instance"/> is in cannot take: there is text, the state runs a human-only Step the catalog knows, and <see cref="Of"/> reads nothing from it. A state that is not a stop is no question.</summary>
    /// <param name="text">The fact's stored text; null when there is none.</param>
    /// <param name="instance">The instance, in the state it has just entered.</param>
    /// <param name="recipe">The recipe it runs, with its blocks' descriptors.</param>
    /// <returns>True when the recommendation is dropped (the caller warns).</returns>
    public static bool Dropped(string? text, WorkflowInstance instance, InterpretedRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(recipe);
        return text is not null && recipe.Recipe.States.TryGetValue(instance.Frame.State, out var state) && state.Step is { } step && recipe.Catalog.Find(step.Use) is { HumanOnly: true } descriptor
            && Of(text, StepOutcomes.Of(descriptor, step, recipe.Recipe) ?? descriptor.Outcomes) is null;
    }
}
