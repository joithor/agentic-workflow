namespace Aw.CodeIndex.Core.Ports;

/// <summary>Scrubs free text before it is stored (spec §9): Aw.Redaction first, then the cut, in that order, so a secret never reaches disk even in a signature.</summary>
public interface IBodyScrubber
{
    /// <summary>Scrubs and cuts a text.</summary>
    /// <param name="text">The raw text.</param>
    /// <param name="maxCharacters">The cut length.</param>
    /// <returns>The scrubbed text, at most <paramref name="maxCharacters"/> long.</returns>
    public string Scrub(string text, int maxCharacters);
}
