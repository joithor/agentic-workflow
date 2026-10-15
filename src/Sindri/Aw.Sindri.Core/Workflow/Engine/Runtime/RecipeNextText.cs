using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Fills a recipe's 'next' variables (engine spec §3.7: '{item}', '{loop.revisions}', '{budget.tokens.left}', …).</summary>
internal static partial class RecipeNextText
{
    /// <summary>
    /// <paramref name="text"/> with each '{variable}' replaced by what <paramref name="value"/> gives for it; a variable it
    /// gives nothing for ('{user}', which status doesn't know) stays as written.
    /// </summary>
    /// <param name="text">The recipe's 'next'.</param>
    /// <param name="value">The value of a variable, or null.</param>
    /// <returns>The filled text.</returns>
    public static string Fill(string text, Func<string, string?> value) =>
        Variable.Replace(text, m => value(m.Groups["name"].Value) ?? m.Value);

    [GeneratedRegex(@"\{(?<name>[a-z][a-z0-9.\-]*)\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Variable { get; }
}
