using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Testing;

/// <summary>An ITextScrubber that replaces each value it was told is secret with '[REDACTED]'.</summary>
public sealed class FakeTextScrubber : ITextScrubber
{
    /// <summary>What replaces a secret.</summary>
    public const string Placeholder = "[REDACTED]";

    private readonly List<string> _secrets = [];

    /// <summary>Treats <paramref name="secret"/> as a secret.</summary>
    /// <param name="secret">The value.</param>
    /// <returns>This scrubber.</returns>
    public FakeTextScrubber Hides(string secret)
    {
        _secrets.Add(secret);
        return this;
    }

    /// <inheritdoc/>
    public string Scrub(string text) =>
        _secrets.Aggregate(text, static (current, secret) => current.Replace(secret, Placeholder, StringComparison.Ordinal));
}
