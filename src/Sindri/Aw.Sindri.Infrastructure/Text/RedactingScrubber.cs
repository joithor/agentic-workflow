using Aw.Redaction;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Infrastructure.Text;

/// <summary>The text port over Aw.Redaction (API spec §3.6; one scrubber for the whole build). A text the redactor can't scan within its timeout comes back as <see cref="ScrubResult.BlockedText"/>, never as it was.</summary>
/// <param name="redactor">The redactor.</param>
internal sealed class RedactingScrubber(IRedactor redactor) : ITextScrubber
{
    public string Scrub(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return redactor.Scrub(text).Text;
    }
}
