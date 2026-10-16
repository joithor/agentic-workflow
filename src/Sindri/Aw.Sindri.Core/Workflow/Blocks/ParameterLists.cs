using System.Text.Json;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>Finds the literal list a Step's OutcomesFrom or InputsFrom names (DT spec §8.1).</summary>
internal static class ParameterLists
{
    /// <summary>The literal list named by <paramref name="from"/>.</summary>
    /// <param name="from">"choices", or "&lt;stateParam&gt;.&lt;listParam&gt;": the text parameter names a state and the list is that state's Step parameter.</param>
    /// <param name="with">The Step's own 'with:' values.</param>
    /// <param name="recipe">The recipe, for the named state.</param>
    /// <returns>The array, or null when it is missing, not literal or not a list.</returns>
    public static JsonElement? Literal(string from, IReadOnlyDictionary<string, WithValue> with, Recipe recipe)
    {
        var dot = from.IndexOf('.', StringComparison.Ordinal);
        if (dot < 0)
        {
            return List(with, from);
        }

        if (!with.TryGetValue(from[..dot], out var named) || named.Literal is not { ValueKind: JsonValueKind.String } state
            || state.GetString() is not { } stateName || !recipe.States.TryGetValue(stateName, out var route) || route.Step is not { } source)
        {
            return null;
        }

        return List(source.With, from[(dot + 1)..]);
    }

    private static JsonElement? List(IReadOnlyDictionary<string, WithValue> with, string name) =>
        with.TryGetValue(name, out var value) && value.Literal is { ValueKind: JsonValueKind.Array } list ? list : null;
}
