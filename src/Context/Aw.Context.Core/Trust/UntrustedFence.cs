using System.Text;

namespace Aw.Context.Core.Trust;

/// <summary>
/// Wraps untrusted text in the pack fence (core spec §8.3): '&lt;untrusted source="…" author="…"&gt;…&lt;/untrusted&gt;'.
/// One escaper serves attribute values and the body, so no field can close its own fence or add an attribute.
/// </summary>
public static class UntrustedFence
{
    /// <summary>The fence around <paramref name="body"/>.</summary>
    /// <param name="source">The source id.</param>
    /// <param name="author">The author, or null to leave the attribute out.</param>
    /// <param name="body">The (already scrubbed) text.</param>
    /// <returns>The fenced text.</returns>
    public static string Wrap(string source, string? author, string body)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(body);
        var fenced = new StringBuilder("<untrusted source=\"").Append(Escape(source)).Append('"');
        if (author is not null)
        {
            _ = fenced.Append(" author=\"").Append(Escape(author)).Append('"');
        }

        return fenced.Append('>').Append(Escape(body)).Append("</untrusted>").ToString();
    }

    /// <summary>The longest title an item carries (spec §7: titles are collapsed to one line; this also bounds them).</summary>
    public const int MaxTitle = 200;

    /// <summary><paramref name="text"/> cut to <paramref name="max"/> characters, with '…' when cut.</summary>
    /// <param name="text">The text.</param>
    /// <param name="max">The most characters kept.</param>
    /// <returns>The capped text.</returns>
    public static string Cap(string text, int max = MaxTitle)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 2);
        return text.Length <= max ? text : string.Concat(Prefix(text, max - 1), "…");
    }

    /// <summary><paramref name="text"/> cut to at most <paramref name="max"/> characters on a word boundary, with '…' when cut (a text with no space is cut like <see cref="Cap"/>).</summary>
    /// <param name="text">The text.</param>
    /// <param name="max">The most characters kept, the ellipsis included.</param>
    /// <returns>The capped text.</returns>
    public static string CapOnWord(string text, int max = MaxTitle)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, 2);
        if (text.Length <= max)
        {
            return text;
        }

        var kept = Prefix(text, max - 1);
        var boundary = char.IsWhiteSpace(text[kept.Length]) ? kept.Length : kept.LastIndexOf(' ');
        return string.Concat(boundary > 0 ? kept[..boundary].TrimEnd() : kept, "…");
    }

    /// <summary>The first <paramref name="length"/> UTF-16 units of <paramref name="text"/>, one fewer when the cut would split a surrogate pair.</summary>
    /// <param name="text">The text.</param>
    /// <param name="length">The most units kept.</param>
    /// <returns>The prefix.</returns>
    public static string Prefix(string text, int length)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        if (text.Length <= length)
        {
            return text;
        }

        return char.IsHighSurrogate(text[length - 1]) ? text[..(length - 1)] : text[..length];
    }

    /// <summary>Escapes '&amp;', '&lt;', '&gt;' and '"', so the text can't open, close or extend a fence.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
