using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Infrastructure.Text;

/// <summary>The text port over Aw.Redaction (API spec §3.6). Red stub: P3 unit A makes RedactingScrubberContractTests pass.</summary>
internal sealed class RedactingScrubber : ITextScrubber
{
    public string Scrub(string text) => throw new NotSupportedException("RedactingScrubber.Scrub: P3 unit A");
}
