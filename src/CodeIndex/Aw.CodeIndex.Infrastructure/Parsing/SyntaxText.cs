using System.Text;

namespace Aw.CodeIndex.Infrastructure.Parsing;

/// <summary>Text helpers for stored signatures.</summary>
internal static class SyntaxText
{
    /// <summary>Collapses every run of whitespace (line breaks included) to one space and trims the ends.</summary>
    /// <param name="text">The source text.</param>
    /// <returns>The normalized text.</returns>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }
}
