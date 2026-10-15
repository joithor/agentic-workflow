using Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// What an instance is about (UI screens §5.4, API spec §3.8): the recipe's first declared input. The key is that input's
/// fact key and is also the value of the 'Subject' search attribute, so the list row and the attribute can't disagree; the
/// label is the file name the human gave when the input is present, else the input's declared label.
/// </summary>
public static class SubjectRefs
{
    /// <summary>The subject of an instance of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">The recipe and its input facts.</param>
    /// <returns>The subject, or null when the recipe declares no input.</returns>
    public static SubjectRef? Of(InterpretedRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (recipe.Recipe.Inputs.Count == 0)
        {
            return null;
        }

        var input = recipe.Recipe.Inputs[0];
        var label = recipe.Inputs.TryGetValue(input.Key, out var fact) ? fact.Name : input.Label ?? Bare(input.Key.Value);
        return new SubjectRef(input.Kind ?? input.Type ?? FactTypes.Payload, input.Key.Value, label, Href: null);
    }

    /// <summary>A fact key without its 'fact:' prefix.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The name.</returns>
    internal static string Bare(string key) => key.StartsWith("fact:", StringComparison.Ordinal) ? key["fact:".Length..] : key;
}
