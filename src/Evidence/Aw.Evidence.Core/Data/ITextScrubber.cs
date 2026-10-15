namespace Aw.Evidence.Core.Data;

/// <summary>Removes secrets from text before it reaches a summary or stderr (Aw.Redaction behind it).</summary>
public interface ITextScrubber
{
    /// <summary>Scrubs <paramref name="text"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with each secret replaced by a placeholder.</returns>
    public string Scrub(string text);

    /// <summary>True when a JSON member or field named <paramref name="name"/> holds a secret whatever its value looks like ('password', 'client_secret', 'accessToken').</summary>
    /// <param name="name">The member name.</param>
    /// <returns>True when the member's whole value must be hidden.</returns>
    public bool IsSecretName(string name);
}
