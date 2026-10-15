using System.Globalization;
using System.Text;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>Checks a question before any source sees it (spec §8.1, §8.3 caps): the text and node shapes; item and symbol anchors arrive in later phases.</summary>
public static class QueryRules
{
    /// <summary>The budget a request gets when it names none.</summary>
    public const int DefaultBudget = 8_000;

    /// <summary>The most search terms a question keeps.</summary>
    public const int MaxTerms = 16;

    /// <summary>The longest question text.</summary>
    public const int MaxTextLength = 2_000;

    /// <summary>The deepest cascade a request may ask for (spec §8.3).</summary>
    public const int MaxDepth = 4;

    /// <summary>Checks <paramref name="query"/>.</summary>
    /// <param name="query">The question.</param>
    /// <param name="settings">The checked settings.</param>
    /// <returns>The terms (none for the node shape: they come from its anchor) and budget; SND-CONTEXT-006 for a malformed question; SND-CONTEXT-003 for the item and symbol shapes.</returns>
    public static Result<ValidQuery> Check(ContextQuery query, ContextSettings settings)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(settings);
        var shapes = new[] { query.Text, query.Node, query.Item, query.Symbol }.Count(s => s is not null);
        if (shapes != 1)
        {
            return ContextErrors.QueryInvalid.With("ask exactly one of: text, --node, --item or --symbol");
        }

        if (query.Depth is < 0 or > MaxDepth || query.Budget is <= 0)
        {
            return ContextErrors.QueryInvalid.With(string.Create(CultureInfo.InvariantCulture,
                $"depth is 0 to {MaxDepth} and budget a positive number of tokens"));
        }

        return Shaped(query, Math.Min(query.Budget ?? DefaultBudget, settings.MaxBudget));
    }

    /// <summary>The one shape's own checks: a node name's length; the text's words; item and symbol not yet (003).</summary>
    private static Result<ValidQuery> Shaped(ContextQuery query, int budget)
    {
        if (query.Node is { } node)
        {
            return string.IsNullOrWhiteSpace(node) || node.Length > MaxTextLength
                ? ContextErrors.QueryInvalid.With(string.Create(CultureInfo.InvariantCulture, $"--node names a node key, alias or name of 1 to {MaxTextLength} characters"))
                : new ValidQuery([], budget);
        }

        if (query.Text is null)
        {
            return ContextErrors.UnknownAnchor.With("item and symbol anchors arrive in later phases; ask by --node or a text question");
        }

        var terms = Terms(query.Text);
        if (query.Text.Length > MaxTextLength || terms.Count == 0)
        {
            return ContextErrors.QueryInvalid.With(string.Create(CultureInfo.InvariantCulture,
                $"the question needs at least one word and at most {MaxTextLength} characters"));
        }

        return new ValidQuery(terms, budget);
    }

    /// <summary>The distinct words of <paramref name="text"/>, as <see cref="Terms"/> reads them but with a cap of the caller's choosing.</summary>
    /// <param name="text">The text.</param>
    /// <param name="max">The most words to return.</param>
    /// <returns>Up to <paramref name="max"/> words, in order of first use.</returns>
    public static IReadOnlyList<string> Words(string text, int max)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var c in text.Append(' '))
        {
            if (char.IsLetterOrDigit(c) && word.Length < 64)
            {
                _ = word.Append(char.ToLowerInvariant(c));
            }
            else if (!char.IsLetterOrDigit(c) && word.Length > 0)
            {
                var term = word.ToString();
                _ = word.Clear();
                if (!words.Contains(term, StringComparer.Ordinal) && words.Count < max)
                {
                    words.Add(term);
                }
            }
        }

        return words;
    }

    /// <summary>The distinct words of <paramref name="text"/>: runs of letters and digits, lower-cased, at most 64 characters each.</summary>
    /// <param name="text">The question.</param>
    /// <returns>Up to <see cref="MaxTerms"/> terms, in order of first use.</returns>
    public static IReadOnlyList<string> Terms(string text) => Words(text, MaxTerms);
}
