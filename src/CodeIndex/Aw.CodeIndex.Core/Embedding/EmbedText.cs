using Aw.CodeIndex.Core.Model;

namespace Aw.CodeIndex.Core.Embedding;

/// <summary>The text one symbol is embedded as (spec §5.6): its name, scrubbed signature and scrubbed body, cut.</summary>
public static class EmbedText
{
    /// <summary>Builds the text.</summary>
    /// <param name="candidate">The symbol and its stored, scrubbed text.</param>
    /// <returns>The text, at most <see cref="CodeIndexRules.EmbedTextMaxCharacters"/> characters.</returns>
    public static string Of(EmbedCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var text = string.Join('\n', candidate.Name, candidate.Signature, candidate.Body);
        return text.Length <= CodeIndexRules.EmbedTextMaxCharacters ? text : text[..CodeIndexRules.EmbedTextMaxCharacters];
    }
}
