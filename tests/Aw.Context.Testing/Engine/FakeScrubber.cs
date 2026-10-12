using Aw.Context.Core.Trust;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The in-memory scrubber for both ports: it replaces <see cref="Secret"/> with '[REDACTED:fake]', withholds any field
/// holding <see cref="BlockMe"/>, drops '&lt;!-- … --&gt;' comments, and collapses single-line fields. It passes
/// <see cref="ScrubberContract{TScrubber}"/>.
/// </summary>
public sealed class FakeScrubber : IEgressScrubber, IIngestFilter
{
    /// <summary>The planted secret it recognizes.</summary>
    public const string Secret = "FAKE-SECRET-1234";

    /// <summary>The text that makes it block a field.</summary>
    public const string BlockMe = "BLOCK-ME";

    /// <inheritdoc />
    public ScrubbedField Scrub(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Contains(BlockMe, StringComparison.Ordinal)
            ? new ScrubbedField("[REDACTED:blocked]", Redacted: false, Blocked: true)
            : new ScrubbedField(value.Replace(Secret, "[REDACTED:fake]", StringComparison.Ordinal), value.Contains(Secret, StringComparison.Ordinal), Blocked: false);
    }

    /// <inheritdoc />
    public ScrubbedField Clean(string raw, bool singleLine)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var text = System.Text.RegularExpressions.Regex.Replace(raw, "<!--.*?-->", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline,
            TimeSpan.FromSeconds(1));
        text = singleLine ? string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) : text;
        return Scrub(text);
    }
}
