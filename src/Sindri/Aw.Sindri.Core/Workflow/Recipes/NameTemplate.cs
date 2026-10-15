using System.Text.RegularExpressions;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// The closed template grammar of a recipe's name and an input's generate (spec §3.11): literal text with
/// {fact:key}, {start:date} and {start:time} placeholders; {{ and }} write a literal brace.
/// Pure: parsing only; rendering is the engine's, because it reads facts.
/// </summary>
internal sealed partial class NameTemplate
{
    private NameTemplate(IReadOnlyList<TemplatePart> parts, string? problem)
    {
        Parts = parts;
        Problem = problem;
    }

    /// <summary>The longest template the validator accepts (RCP-NAME-002).</summary>
    public const int MaxLength = 200;

    /// <summary>The parts in order; empty when <see cref="Problem"/> is set.</summary>
    public IReadOnlyList<TemplatePart> Parts { get; }

    /// <summary>The first problem (RCP-NAME-003): "unknown placeholder '{x}'" or "unbalanced '{' or '}'"; null when the template is well formed.</summary>
    public string? Problem { get; }

    /// <summary>The distinct facts the template reads, in order of first use.</summary>
    public IReadOnlyList<FactKey> Facts => [.. Parts.OfType<FactPart>().Select(p => p.Key).Distinct()];

    /// <summary>Parses a template.</summary>
    /// <param name="text">The template text.</param>
    /// <returns>The parse; check <see cref="Problem"/>.</returns>
    public static NameTemplate Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parts = new List<TemplatePart>();
        foreach (Match match in Token.Matches(text))
        {
            var part = PartOf(match);
            if (part.Part is not { } built)
            {
                return new NameTemplate([], part.Problem);
            }

            if (built is LiteralPart literal && parts.Count > 0 && parts[^1] is LiteralPart previous)
            {
                parts[^1] = new LiteralPart(previous.Text + literal.Text);
            }
            else
            {
                parts.Add(built);
            }
        }

        return new NameTemplate(parts, problem: null);
    }

    [GeneratedRegex(@"\{\{|\}\}|\{(?<body>[^{}]*)\}|[{}]|[^{}]+", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Token { get; }

    private static (TemplatePart? Part, string? Problem) PartOf(Match match) => match.Value switch
    {
        "{{" => (new LiteralPart("{"), null),
        "}}" => (new LiteralPart("}"), null),
        "{" or "}" => (null, "unbalanced '{' or '}'"),
        _ when match.Groups["body"].Success => Placeholder(match.Groups["body"].Value) is { } placeholder
            ? (placeholder, null)
            : (null, $"unknown placeholder '{match.Value}'"),
        _ => (new LiteralPart(match.Value), null),
    };

    private static TemplatePart? Placeholder(string body) => body switch
    {
        "start:date" => new StartPart(IsDate: true),
        "start:time" => new StartPart(IsDate: false),
        _ when body.StartsWith("fact:", StringComparison.Ordinal) && FactKey.TryFrom(body, out var key) => new FactPart(key),
        _ => null,
    };

    /// <summary>One part of a parsed template.</summary>
    internal abstract record TemplatePart;

    /// <summary>Literal text, with the doubled braces already resolved.</summary>
    /// <param name="Text">The text.</param>
    internal sealed record LiteralPart(string Text) : TemplatePart;

    /// <summary>A fact placeholder.</summary>
    /// <param name="Key">The input's fact key.</param>
    internal sealed record FactPart(FactKey Key) : TemplatePart;

    /// <summary>A start-time placeholder.</summary>
    /// <param name="IsDate">True for the date, false for the time.</param>
    internal sealed record StartPart(bool IsDate) : TemplatePart;
}
