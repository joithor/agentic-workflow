using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// Who stands behind an item (SCP-TRUST-001). 'owner' only on a verifiable match: the item cites only answers, and its text equals the
/// 'yields' of an option the log records as chosen, or every quoted span in it is a substring of the 'other' text of a cited batch.
/// Anything else, a mixed citation included, is 'untrusted'. Code stamps it at the evidence step; models never write it.
/// </summary>
internal static partial class ScopeTrust
{
    internal const string Untrusted = "untrusted";
    internal const string Owner = "owner";

    [GeneratedRegex("^A[1-9][0-9]*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AnswerId { get; }

    [GeneratedRegex("[\"“](?<span>[^\"”]+)[\"”]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Quoted { get; }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Spaces { get; }

    /// <summary>The trust label for an item with this text and these citations.</summary>
    /// <param name="text">The item's text.</param>
    /// <param name="citations">The ids it cites.</param>
    /// <param name="facts">The facts holding the answer log and the questions.</param>
    internal static string For(string text, IReadOnlyList<string> citations, ScopeFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (citations.Count == 0 || !citations.All(static c => AnswerId.IsMatch(c)))
        {
            return Untrusted;
        }

        var entries = ScopeJson.Items(facts["answerLog"]).Where(e => citations.Contains(ScopeJson.Str(e, "id") ?? string.Empty, StringComparer.Ordinal)).ToList();
        if (entries.Count == 0)
        {
            return Untrusted;
        }

        var yields = entries.SelectMany(e => ScopeJson.Items(e, "answers")).Select(a => Yields(a, facts)).OfType<string>().ToList();
        if (yields.Exists(y => string.Equals(Normalize(y), Normalize(text), StringComparison.Ordinal)))
        {
            return Owner;
        }

        var spans = Quoted.Matches(text).Select(static m => m.Groups["span"].Value).ToList();
        var others = entries.SelectMany(e => ScopeJson.Items(e, "answers")).Select(static a => ScopeJson.Str(a, "other")).OfType<string>().ToList();
        return spans.Count > 0 && spans.TrueForAll(span => others.Exists(o => o.Contains(span, StringComparison.Ordinal))) ? Owner : Untrusted;
    }

    private static string? Yields(JsonNode answer, ScopeFacts facts)
    {
        var option = ScopeJson.Str(answer, "option");
        return option is null
            ? null
            : ScopeJson.Items(facts["questions"]).SelectMany(static q => ScopeJson.Items(q, "options"))
                .FirstOrDefault(o => string.Equals(ScopeJson.Str(o, "id"), option, StringComparison.Ordinal)) is { } chosen
                ? ScopeJson.Str(chosen, "yields")
                : null;
    }

    private static string Normalize(string text) => Spaces.Replace(text.Trim(), " ");
}
