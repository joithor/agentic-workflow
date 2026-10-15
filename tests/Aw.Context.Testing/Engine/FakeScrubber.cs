using System.Text.RegularExpressions;
using Aw.Context.Core.Trust;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The in-memory scrubber for both ports: it replaces <see cref="Secret"/> with '[REDACTED:fake]' and each extra
/// pattern's matches with '[REDACTED:&lt;id&gt;]', withholds any field holding <see cref="BlockMe"/>, drops
/// '&lt;!-- … --&gt;' comments, and collapses single-line fields. It passes <see cref="ScrubberContract"/>.
/// </summary>
/// <param name="extra">The extra patterns, by rule id (none by default).</param>
public sealed class FakeScrubber(IReadOnlyList<(string Id, Regex Pattern)>? extra = null) : IEgressScrubber, IIngestFilter
{
    /// <summary>The planted secret it recognizes.</summary>
    public const string Secret = "FAKE-SECRET-1234";

    /// <summary>The text that makes it block a field.</summary>
    public const string BlockMe = "BLOCK-ME";

    private readonly IReadOnlyList<(string Id, Regex Pattern)> _extra = extra ?? [];

    /// <inheritdoc />
    public ScrubbedField Scrub(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Contains(BlockMe, StringComparison.Ordinal))
        {
            return new ScrubbedField("[REDACTED:blocked]", Redacted: false, Blocked: true);
        }

        var text = value.Replace(Secret, "[REDACTED:fake]", StringComparison.Ordinal);
        foreach (var (id, pattern) in _extra)
        {
            text = pattern.Replace(text, "[REDACTED:" + id + "]");
        }

        return new ScrubbedField(text, !string.Equals(text, value, StringComparison.Ordinal), Blocked: false);
    }

    /// <inheritdoc />
    public ScrubbedField Clean(string raw, bool singleLine)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var text = Regex.Replace(raw, "<!--.*?-->", string.Empty, RegexOptions.Singleline, TimeSpan.FromSeconds(1));
        text = singleLine ? string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) : text;
        return Scrub(text);
    }
}
