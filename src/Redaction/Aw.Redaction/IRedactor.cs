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

    /// <summary>
    /// True when a field, header or attribute named <paramref name="name"/> holds a secret whatever its value looks
    /// like (client_secret, X-Api-Key, accessToken, Authorization, Cookie). A caller that scrubs a key and its value
    /// separately, such as a telemetry processor, redacts the whole value of such a field: the assignment rules can't
    /// see the name once it is split from the value.
    /// </summary>
    /// <param name="name">The field, header or attribute name.</param>
    /// <returns>True when the value should be redacted whole.</returns>
    public bool IsSecretName(string name);
}
