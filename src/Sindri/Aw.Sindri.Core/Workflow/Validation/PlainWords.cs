using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The plain-words check behind RCP-DISPLAY-001, -002 and -003 (setup-once spec §5.3): a text the app shows to the owner is one line, no longer than its
/// limit, and holds no id, code name or file-format token. Pure; it returns the first problem as a sentence, or null.
/// </summary>
internal static partial class PlainWords
{
    /// <summary>The longest display name (a recipe's or a state's) and transition label.</summary>
    internal const int MaxName = 60;

    /// <summary>The longest <c language="text">why</c>.</summary>
    internal const int MaxWhy = 200;

    /// <summary>The first problem with <paramref name="text"/>, or null when it is plain words.</summary>
    /// <param name="text">The text as written.</param>
    /// <param name="max">The longest it may be.</param>
    /// <param name="sentenceCase">Whether it must start with a capital letter and not end in a full stop.</param>
    /// <returns>The problem, or null.</returns>
    internal static string? Problem(string text, int max, bool sentenceCase)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "it is empty";
        }

        if (text.Length > max)
        {
            return string.Create(CultureInfo.InvariantCulture, $"it is {text.Length} characters, over the limit of {max}");
        }

        if (text.Any(char.IsControl))
        {
            return "it must be one line of text";
        }

        if (sentenceCase && !char.IsUpper(text[0]))
        {
            return "it doesn't start with a capital letter (sentence case)";
        }

        if (sentenceCase && text.EndsWith('.'))
        {
            return "it ends with a full stop (a name is not a sentence)";
        }

        return Token.Match(text) is { Success: true } token
            ? $"'{token.Value}' is an id or code name, not plain words"
            : null;
    }

    // why: ids (workflow.x, step.x.y, fact:x), code names (RCP-X-001, SND-X-001), hashes, backticks, snake_case and camelCase or PascalCase identifiers
    [GeneratedRegex(@"\b(?:workflow|step|gate|effect|evidence|input|fact)[.:][a-z][A-Za-z0-9-]*(?:\.[a-z][A-Za-z0-9-]*)*|\b[A-Z]{2,5}-[A-Z]+-\d+\b|sha256|`|\w_\w|\b[a-z]+[A-Z]\w*|\b[A-Z][a-z]+[A-Z]\w*",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Token { get; }
}
