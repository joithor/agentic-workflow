using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Evidence.Core.Lint;

/// <summary>Deterministic sloppiness lint over a page snapshot; no model involved.</summary>
public static partial class PageLint
{
    // A rough average glyph width at UI font sizes: enough to catch clearly-too-long text, not pixel-exact.
    private const int AverageGlyphPx = 7;
    private const int TruncatedLabelMinLength = 20;
    private const double MisalignedEdgeMaxPx = 4;

    /// <summary>Runs every rule.</summary>
    /// <param name="snapshot">The page.</param>
    /// <param name="allowedColors">Allowed colour values (case-insensitive); null skips the colour rule entirely, so an unconfigured caller never gets a false positive.</param>
    /// <returns>The findings, pair rules first.</returns>
    public static IReadOnlyList<LintFinding> Check(PageSnapshot snapshot, IReadOnlyCollection<string>? allowedColors = null)
    {
        var findings = new List<LintFinding>();
        var elements = snapshot.Elements;
        for (var i = 0; i < elements.Count; i++)
        {
            for (var j = i + 1; j < elements.Count; j++)
            {
                CheckPair(elements[i], elements[j], findings);
            }
        }

        var allowed = allowedColors?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var element in elements)
        {
            CheckElement(element, snapshot.Viewport.W, findings);
            if (allowed is not null && element.Style.Color is { } color && !allowed.Contains(color))
            {
                findings.Add(new(LintRule.NonTokenColor, element.Selector, $"color {color} is not an allowed token value"));
            }
        }

        return findings;
    }

    private static void CheckPair(PageElement a, PageElement b, List<LintFinding> findings)
    {
        var pair = $"{a.Selector} / {b.Selector}";
        if (a.Rect.Overlaps(b.Rect))
        {
            findings.Add(new(LintRule.Overlap, pair, "bounding boxes overlap"));
        }

        var edge = Math.Abs(a.Rect.X - b.Rect.X);
        if (edge is > 0 and <= MisalignedEdgeMaxPx)
        {
            findings.Add(new(LintRule.MisalignedEdge, pair, Invariant($"left edges differ by {edge}px")));
        }
    }

    private static void CheckElement(PageElement e, int viewportWidth, List<LintFinding> findings)
    {
        var ellipsis = string.Equals(e.Style.TextOverflow, "ellipsis", StringComparison.Ordinal);
        var textWidth = e.Text.Length * AverageGlyphPx;
        Add(findings, e.Rect.Right > viewportWidth, LintRule.PhoneOverflow, e, Invariant($"right edge at {e.Rect.Right}px exceeds viewport width {viewportWidth}px"));
        Add(findings, ellipsis && e.Text.Length > TruncatedLabelMinLength, LintRule.TruncatedLabel, e, $"label \"{e.Text}\" is truncated by ellipsis");
        Add(findings, string.Equals(e.Style.Overflow, "hidden", StringComparison.Ordinal) && !ellipsis && textWidth > e.Rect.W,
            LintRule.Clipped, e, Invariant($"overflow:hidden with text ~{textWidth}px wide in a {e.Rect.W}px box"));
        Add(findings, Interactive.IsMatch(e.Selector) && (!e.HasHoverState || !e.HasFocusState),
            LintRule.MissingHoverFocus, e, Invariant($"hasHoverState={e.HasHoverState} hasFocusState={e.HasFocusState}"));
        Add(findings, Placeholder.IsMatch(e.Text), LintRule.UnresolvedPlaceholder, e, $"rendered text \"{e.Text}\" looks like an unresolved placeholder");
    }

    private static void Add(List<LintFinding> findings, bool hit, LintRule rule, PageElement e, string detail)
    {
        if (hit)
        {
            findings.Add(new(rule, e.Selector, detail));
        }
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex("^(button|a\\b|\\[role=[\"']?button[\"']?\\]|input)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Interactive { get; }

    [GeneratedRegex("^\\s*(loading\\.{0,3}|undefined|null|nan)\\s*\\z|:\\s*(undefined|null|nan)\\s*\\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Placeholder { get; }
}
