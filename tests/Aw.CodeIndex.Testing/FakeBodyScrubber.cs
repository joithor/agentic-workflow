using Aw.CodeIndex.Core.Ports;

namespace Aw.CodeIndex.Testing;

/// <summary>Cuts text to the length asked and replaces every configured secret with '[redacted]': enough to prove a caller scrubs before it stores.</summary>
/// <param name="secrets">The literals to hide.</param>
public sealed class FakeBodyScrubber(params string[] secrets) : IBodyScrubber
{
    /// <inheritdoc />
    public string Scrub(string text, int maxCharacters)
    {
        var scrubbed = secrets.Aggregate(text, (current, secret) => current.Replace(secret, "[redacted]", StringComparison.Ordinal));
        return scrubbed.Length <= maxCharacters ? scrubbed : scrubbed[..maxCharacters];
    }
}
