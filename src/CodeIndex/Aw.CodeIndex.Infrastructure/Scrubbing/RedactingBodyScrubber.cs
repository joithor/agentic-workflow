using Aw.CodeIndex.Core.Ports;
using Aw.Redaction;

namespace Aw.CodeIndex.Infrastructure.Scrubbing;

/// <summary>
/// The scrubber behind <see cref="IBodyScrubber"/> (spec §9): Aw.Redaction first, then the cut, in that order, so a secret
/// cut in half by the limit is still redacted whole. A text the redactor withheld (a pattern ran past its time limit) is stored as
/// the redactor's placeholder, never as the input.
/// </summary>
/// <param name="redactor">The redactor, with its built-in rules and the profile's extra patterns.</param>
internal sealed class RedactingBodyScrubber(IRedactor redactor) : IBodyScrubber
{
    /// <summary>Creates the scrubber with the built-in rules.</summary>
    public RedactingBodyScrubber()
        : this(Redactor.Default)
    {
    }

    /// <inheritdoc />
    public string Scrub(string text, int maxCharacters)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(maxCharacters);
        var scrubbed = redactor.Scrub(text).Text;
        return scrubbed.Length <= maxCharacters ? scrubbed : scrubbed[..maxCharacters];
    }
}
