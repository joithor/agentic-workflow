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

    private const string DepthRule = "depth must be a whole number from 0 to 4";
    private const string BudgetRule = "budget must be a whole number of tokens, 1 or more";

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

        if (CheckDepth(query.Depth) is { } depthError)
        {
            return depthError;
        }

        if (query.Budget is <= 0 and var budget)
        {
            return OutOfRange(BudgetRule, budget.ToString(CultureInfo.InvariantCulture));
        }

        return Shaped(query, Math.Min(query.Budget ?? DefaultBudget, settings.MaxBudget));
    }

    /// <summary>A depth as a transport sent it: absent, or a whole number from 0 to <see cref="MaxDepth"/>.</summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The depth (null when absent); SND-CONTEXT-006 when it is not a whole number or is out of range. The message names no flag: a transport adds its own hint.</returns>
    public static Result<int?> ParseDepth(string? text) => ParseWhole(text, DepthRule, 0, MaxDepth);

    /// <summary>A token budget as a transport sent it: absent, or a whole number of 1 or more.</summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The budget (null when absent); SND-CONTEXT-006 when it is not a whole number or is out of range.</returns>
    public static Result<int?> ParseBudget(string? text) => ParseWhole(text, BudgetRule, 1, int.MaxValue);

    /// <summary>The 006 for a depth already a number, worded as <see cref="ParseDepth"/> words it.</summary>
    /// <param name="depth">The depth, or null.</param>
    /// <returns>Null when absent or 0 to <see cref="MaxDepth"/>, else the error.</returns>
    public static Error? CheckDepth(int? depth) =>
        depth is { } value && value is < 0 or > MaxDepth ? OutOfRange(DepthRule, value.ToString(CultureInfo.InvariantCulture)) : null;

    private static Result<int?> ParseWhole(string? text, string rule, int min, int max)
    {
        if (text is null)
        {
            return (int?)null;
        }

        var digits = text.Length > 0 && text[0] is '-' or '+' ? text[1..] : text;
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit))
        {
            return ContextErrors.QueryInvalid.With(rule);
        }

        if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max)
        {
            return value;
        }

        return OutOfRange(rule, text);
    }

    private static Error OutOfRange(string rule, string shown) =>
        ContextErrors.QueryInvalid.With($"{rule}; {(shown.Length <= 12 ? shown : "that value")} is out of range");

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
