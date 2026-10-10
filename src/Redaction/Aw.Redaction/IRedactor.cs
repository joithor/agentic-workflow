namespace Aw.Redaction;

/// <summary>Finds secret and identifier shapes in text and replaces each with a [REDACTED:rule] placeholder.</summary>
public interface IRedactor
{
    /// <summary>
    /// Scrubs <paramref name="text"/>. A pattern that runs past its match timeout blocks: the result carries no part of
    /// the input (<see cref="ScrubOutcome.Blocked"/>), so a caller can never pass unscrubbed text on by mistake.
    /// </summary>
    /// <param name="text">The text to scrub.</param>
    /// <returns>The scrubbed text, the hits (rule id and span, never the matched value) and the outcome.</returns>
    public ScrubResult Scrub(string text);
}
