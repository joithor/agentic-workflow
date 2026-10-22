using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Features.Line;

internal static partial class DesignChecks
{
    private const string StyleRule = "DSN-STYLE-001";

    [GeneratedRegex(@"^## (?<text>.+?)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StyleHeading { get; }

    // DSN-STYLE-001: spec.md (design.spec) follows the pinned style pack: required sections present and in order, each within its word limit, no forbidden phrase, within the line cap.
    private static IEnumerable<LineFinding> StyleFindings(DesignContext c, StylePack style)
    {
        var spec = LineJson.Str(c.Design, "spec");
        if (string.IsNullOrWhiteSpace(spec))
        {
            yield return DesignContext.Fail(StyleRule, item: null, "the design has no spec.md text to check against the style pack", "Write the spec in the style pack's template.");
            yield break;
        }

        var heads = StyleHeading.Matches(spec);
        foreach (var finding in StyleSections(spec, heads, style.Sections).Concat(StyleUnknown(heads, style.Sections)).Concat(StyleWording(spec, style.Rules)))
        {
            yield return finding;
        }
    }

    private static IEnumerable<LineFinding> StyleUnknown(MatchCollection heads, IReadOnlyList<StyleSection> sections) =>
        heads.Select(static h => h.Groups["text"].Value)
            .Where(text => !sections.Any(s => string.Equals(s.Heading, text, StringComparison.OrdinalIgnoreCase)))
            .Select(static text => DesignContext.Fail(StyleRule, text, $"unknown section '{text}'", "Use only the style pack's sections."));

    private static IEnumerable<LineFinding> StyleSections(string spec, MatchCollection heads, IReadOnlyList<StyleSection> sections)
    {
        var last = -1;
        foreach (var section in sections)
        {
            var index = StyleIndex(heads, section.Heading);
            if (index < 0)
            {
                if (section.Required)
                {
                    yield return DesignContext.Fail(StyleRule, section.Heading, $"missing required section '{section.Heading}'", $"Add a '## {section.Heading}' section.");
                }

                continue;
            }

            if (index < last)
            {
                yield return DesignContext.Fail(StyleRule, section.Heading, $"section '{section.Heading}' is out of order", "Put the sections in the style pack's order.");
            }

            last = Math.Max(last, index);
            var words = StyleWords(spec, heads, index);
            if (section.MaxWords is { } max && words > max)
            {
                yield return DesignContext.Fail(StyleRule, section.Heading, $"section '{section.Heading}' has {words.ToString(CultureInfo.InvariantCulture)} words, over {max.ToString(CultureInfo.InvariantCulture)}", "Shorten the section.");
            }
        }
    }

    private static IEnumerable<LineFinding> StyleWording(string spec, StyleRules rules)
    {
        foreach (var phrase in rules.Forbidden)
        {
            if (Regex.IsMatch(spec, $@"(^|[^\p{{L}}\p{{N}}'])({Regex.Escape(phrase)})($|[^\p{{L}}\p{{N}}'])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            {
                yield return DesignContext.Fail(StyleRule, item: null, $"forbidden phrase '{phrase}'", "Reword it.");
            }
        }

        var lines = spec.Split('\n').Length - (spec.EndsWith('\n') ? 1 : 0);
        if (rules.MaxLines is { } cap && lines > cap)
        {
            yield return DesignContext.Fail(StyleRule, item: null, $"{lines.ToString(CultureInfo.InvariantCulture)} lines, over {cap.ToString(CultureInfo.InvariantCulture)}", "Cut the spec to the style pack's line cap.");
        }
    }

    private static int StyleIndex(MatchCollection heads, string heading)
    {
        for (var i = 0; i < heads.Count; i++)
        {
            if (string.Equals(heads[i].Groups["text"].Value, heading, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    // The words under heading 'index', up to the next '## ' heading.
    private static int StyleWords(string spec, MatchCollection heads, int index)
    {
        var start = heads[index].Index + heads[index].Length;
        var end = index + 1 < heads.Count ? heads[index + 1].Index : spec.Length;
        return spec[start..end].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
