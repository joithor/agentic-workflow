namespace Aw.Sindri.Core.Text;

/// <summary>Takes secrets out of text from humans, trackers and agents before a projection stores it (API spec §3.6; an adapter over Aw.Redaction).</summary>
public interface ITextScrubber
{
    /// <summary>The text with every secret it recognises replaced.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The scrubbed text.</returns>
    public string Scrub(string text);
}
