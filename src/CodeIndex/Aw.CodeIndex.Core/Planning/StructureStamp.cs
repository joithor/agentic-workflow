using System.Security.Cryptography;
using System.Text;

namespace Aw.CodeIndex.Core.Planning;

/// <summary>The structure layer's stamp: what changes the rows a parse stores (spec §5.2).</summary>
public static class StructureStamp
{
    /// <summary>Builds the stamp from the parser's id and the settings that shape stored rows.</summary>
    /// <param name="parserVersion">The parser's version label.</param>
    /// <param name="utilityGlobs">The profile's utility globs.</param>
    /// <param name="scrubPatterns">The profile's extra scrub patterns.</param>
    /// <returns>The parser id and the first 8 hex digits of a hash of the two lists; order and repeats don't matter.</returns>
    public static string Compute(string parserVersion, IReadOnlyList<string> utilityGlobs, IReadOnlyList<string> scrubPatterns)
    {
        ArgumentException.ThrowIfNullOrEmpty(parserVersion);
        ArgumentNullException.ThrowIfNull(utilityGlobs);
        ArgumentNullException.ThrowIfNull(scrubPatterns);
        var text = new StringBuilder("utility\n");
        AppendSorted(text, utilityGlobs);
        _ = text.Append("scrub\n");
        AppendSorted(text, scrubPatterns);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return $"{parserVersion}:{Convert.ToHexStringLower(hash.AsSpan(0, 4))}";
    }

    private static void AppendSorted(StringBuilder text, IReadOnlyList<string> values)
    {
        foreach (var value in values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            _ = text.Append(value.Length).Append(':').Append(value).Append('\n');
        }
    }
}
