using System.Text;
using Aw.Context.Core.Trust;
using Aw.Redaction;

namespace Aw.Context.Infrastructure.Scrubbing;

/// <summary>
/// Both scrubbing ports over Aw.Redaction (spec §7, D14: one scrubber for the whole build). Egress scrubs one field; a
/// blocked field is <see cref="ScrubResult.BlockedText"/> in that field only. Ingest sanitizes first, collapses a
/// single-line field, then scrubs. A multi-line field keeps its line count: a redacted span that covered newlines is
/// '[REDACTED:rule]' followed by the same newlines, so a whole document cleaned at once still lines up with its blob
/// (documents are cleaned whole, then chunked: a secret or comment across a chunk boundary can't slip through).
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
        return singleLine ? Scrub(string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))) : ScrubKeepingLines(text);
    }

    private ScrubbedField ScrubKeepingLines(string text)
    {
        var result = redactor.Scrub(text);
        if (result.IsBlocked || result.Outcome == ScrubOutcome.Clean)
        {
            return result.IsBlocked ? new ScrubbedField(ScrubResult.BlockedText, Redacted: false, Blocked: true) : new ScrubbedField(text, Redacted: false, Blocked: false);
        }

        var kept = new StringBuilder(text.Length);
        var at = 0;
        foreach (var hit in result.Hits)
        {
            var span = text[hit.Start..hit.End];
            _ = kept.Append(text, at, hit.Start - at).Append("[REDACTED:").Append(hit.Rule).Append(']').Append(IngestSanitizer.Newlines(span));
            at = hit.End;
        }

        return new ScrubbedField(kept.Append(text, at, text.Length - at).ToString(), Redacted: true, Blocked: false);
    }
}
