using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Testing;

/// <summary>An ITextScrubber that replaces each value it was told is secret with '[REDACTED]'.</summary>
public sealed class FakeTextScrubber : ITextScrubber
{
    /// <summary>What replaces a secret.</summary>
    public const string Placeholder = "[REDACTED]";

    private readonly List<string> _secrets = [];
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Treats <paramref name="secret"/> as a secret.</summary>
    /// <param name="secret">The value.</param>
    /// <returns>This scrubber.</returns>
    public FakeTextScrubber Hides(string secret)
    {
        _secrets.Add(secret);
        return this;
    }

    /// <summary>Treats a JSON member called <paramref name="name"/> as holding a secret.</summary>
    /// <param name="name">The member name.</param>
    /// <returns>This scrubber.</returns>
    public FakeTextScrubber HidesName(string name)
    {
        _ = _names.Add(name);
        return this;
    }

    /// <inheritdoc/>
    public bool IsSecretName(string name) => _names.Contains(name);

    /// <inheritdoc/>
    public string Scrub(string text) =>
        _secrets.Aggregate(text, static (current, secret) => current.Replace(secret, Placeholder, StringComparison.Ordinal));
}
