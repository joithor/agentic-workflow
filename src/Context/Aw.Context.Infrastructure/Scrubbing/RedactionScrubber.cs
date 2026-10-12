using Aw.Context.Core.Trust;
using Aw.Redaction;

namespace Aw.Context.Infrastructure.Scrubbing;

/// <summary>
/// Both scrubbing ports over Aw.Redaction (spec §7, D14: one scrubber for the whole build). Egress scrubs one field; a
/// blocked field is <see cref="ScrubResult.BlockedText"/> in that field only. Ingest sanitizes first, collapses a
/// single-line field, then scrubs. S-2W passes a redactor with the profile's extra patterns.
/// </summary>
/// <param name="redactor">The redactor.</param>
internal sealed class RedactionScrubber(IRedactor redactor) : IEgressScrubber, IIngestFilter
{
    public ScrubbedField Scrub(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = redactor.Scrub(value);
        return result.IsBlocked
            ? new ScrubbedField(ScrubResult.BlockedText, Redacted: false, Blocked: true)
            : new ScrubbedField(result.Text, result.Outcome == ScrubOutcome.Redacted, Blocked: false);
    }

    public ScrubbedField Clean(string raw, bool singleLine)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var text = IngestSanitizer.Clean(raw);
        return Scrub(singleLine ? string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) : text);
    }
}
