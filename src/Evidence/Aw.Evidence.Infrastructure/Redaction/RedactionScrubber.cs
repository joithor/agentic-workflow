using Aw.Evidence.Core.Data;
using Aw.Redaction;

namespace Aw.Evidence.Infrastructure.Redaction;

/// <summary>Scrubs text with Aw.Redaction's built-in rules. A text the scanner can't finish within its timeout comes back as the blocked placeholder, never as is.</summary>
internal sealed class RedactionScrubber(IRedactor redactor) : ITextScrubber
{
    /// <inheritdoc/>
    public string Scrub(string text) => redactor.Scrub(text).Text;
}
