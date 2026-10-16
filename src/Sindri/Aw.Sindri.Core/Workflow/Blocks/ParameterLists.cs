using System.Text.Json;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>Finds the literal list a Step's OutcomesFrom or InputsFrom names (DT spec §8.1).</summary>
internal static class ParameterLists
{
    /// <summary>Whether <paramref name="outcomesFrom"/> names another state's list ("from.choices"): the Step is a desk twin; otherwise it is a route, listing its own choices.</summary>
    /// <param name="outcomesFrom">A descriptor's OutcomesFrom.</param>
    /// <returns>True for a twin.</returns>
    public static bool IsTwin(string outcomesFrom) => outcomesFrom.Contains('.', StringComparison.Ordinal);

    /// <summary>The Step parameter that names the route state of a desk twin ("from" for "from.choices").</summary>
    /// <param name="outcomesFrom">A twin descriptor's OutcomesFrom ("&lt;stateParam&gt;.&lt;listParam&gt;").</param>
    /// <returns>The state parameter's name.</returns>
    public static string StateParameter(string outcomesFrom) => outcomesFrom[..outcomesFrom.IndexOf('.', StringComparison.Ordinal)];

    /// <summary>The literal state name a desk twin's state parameter holds.</summary>
    /// <param name="outcomesFrom">A twin descriptor's OutcomesFrom.</param>
    /// <param name="with">The twin Step's own 'with:' values.</param>
    /// <returns>The name, or null when the parameter is missing or not a literal string.</returns>
    public static string? NamedState(string outcomesFrom, IReadOnlyDictionary<string, WithValue> with) =>
        with.TryGetValue(StateParameter(outcomesFrom), out var named) && named.Literal is { ValueKind: JsonValueKind.String } state ? state.GetString() : null;

    /// <summary>The literal list named by <paramref name="from"/>.</summary>
    /// <param name="from">"choices", or "&lt;stateParam&gt;.&lt;listParam&gt;": the text parameter names a state and the list is that state's Step parameter.</param>
    /// <param name="with">The Step's own 'with:' values.</param>
    /// <param name="recipe">The recipe, for the named state.</param>
    /// <returns>The array, or null when it is missing, not literal or not a list.</returns>
    public static JsonElement? Literal(string from, IReadOnlyDictionary<string, WithValue> with, Recipe recipe)
    {
        if (!IsTwin(from))
        {
            return List(with, from);
        }

        if (NamedState(from, with) is not { } stateName || !recipe.States.TryGetValue(stateName, out var route) || route.Step is not { } source)
        {
            return null;
        }

        return List(source.With, from[(from.IndexOf('.', StringComparison.Ordinal) + 1)..]);
    }

    private static JsonElement? List(IReadOnlyDictionary<string, WithValue> with, string name) =>
        with.TryGetValue(name, out var value) && value.Literal is { ValueKind: JsonValueKind.Array } list ? list : null;
}
