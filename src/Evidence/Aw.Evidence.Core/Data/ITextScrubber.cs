namespace Aw.Evidence.Core.Data;

/// <summary>Removes secrets from text before it reaches a summary or stderr (Aw.Redaction behind it).</summary>
public interface ITextScrubber
{
    /// <summary>Scrubs <paramref name="text"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with each secret replaced by a placeholder.</returns>
    public string Scrub(string text);
}
