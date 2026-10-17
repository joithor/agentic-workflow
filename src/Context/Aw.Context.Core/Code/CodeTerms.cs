namespace Aw.Context.Core.Code;

/// <summary>The search terms for a data thing (S-CL): a name in four forms, case-insensitive, at least <see cref="CodeRules.MinTermLength"/> characters.</summary>
public static class CodeTerms
{
    /// <summary>The name part of a node key ('thing:shared-link' gives 'shared-link').</summary>
    /// <param name="key">The node key.</param>
    /// <returns>The part after the first ':', or the key itself.</returns>
    public static string KeyName(string key) => throw new NotSupportedException();

    /// <summary>The searchable forms of some names (a key name, a label, aliases): words split on '-_./' and space and on camel boundaries, written as kebab, snake, joined and spaced, lower-cased.</summary>
    /// <param name="names">The names.</param>
    /// <returns>The distinct forms of at least <see cref="CodeRules.MinTermLength"/> characters, in order; empty when none qualifies (the thing is not searchable).</returns>
    public static IReadOnlyList<string> Forms(IEnumerable<string> names) => throw new NotSupportedException();
}
