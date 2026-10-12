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
