using System.Text;

namespace Aw.Context.Core.Concepts;

/// <summary>Concept ids: lower-case words joined by '-', unique within one derivation.</summary>
internal static class ConceptSlug
{
    internal static string Unique(string text, HashSet<string> taken)
    {
        var slug = Of(text);
        var candidate = slug;
        for (var n = 2; !taken.Add(candidate); n++)
        {
            candidate = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{slug}-{n}");
        }

        return candidate;
    }

    private static string Of(string text)
    {
        var slug = new StringBuilder();
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            var keep = char.IsLetterOrDigit(c) || c == '.';
            if (keep)
            {
                _ = slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                _ = slug.Append('-');
            }
        }

        var cleaned = slug.ToString().Trim('-', '.');
        return cleaned.Length == 0 ? "concept" : cleaned;
    }
}
