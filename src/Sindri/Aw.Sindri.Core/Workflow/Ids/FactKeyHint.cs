namespace Aw.Sindri.Core.Workflow.Ids;

/// <summary>The fix a fact key written in the wrong case gets (fact names are camelCase).</summary>
public static class FactKeyHint
{
    /// <summary>The fix for <paramref name="text"/>, which isn't a fact key.</summary>
    /// <param name="text">What the recipe wrote.</param>
    /// <returns>"fact names are camelCase: write fact:evidenceSummary" when the text is a fact key but for its case or hyphens; otherwise "write fact:&lt;camelCaseName&gt;".</returns>
    public static string Fix(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var body = text.StartsWith("fact:", StringComparison.Ordinal) ? text[5..] : text;
        var words = body.Split(['-', '_', ' ', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return Generic;
        }

        var camel = char.ToLowerInvariant(words[0][0]) + words[0][1..]
            + string.Concat(words.Skip(1).Select(static w => char.ToUpperInvariant(w[0]) + w[1..]));
        return FactKey.TryFrom("fact:" + camel, out _) ? $"fact names are camelCase: write fact:{camel}" : Generic;
    }

    private const string Generic = "write fact:<camelCaseName>";
}
