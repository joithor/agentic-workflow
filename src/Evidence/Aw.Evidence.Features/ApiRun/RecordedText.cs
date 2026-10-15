using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>Text on its way into a summary: scrubbed first, then capped, so a cap never cuts a secret in half and leaves the rest unrecognisable.</summary>
internal static class RecordedText
{
    internal const int OutputLimit = 2000;
    internal const int ValueLimit = 200;
    internal const int HeadlineLimit = 200;

    internal static string Of(ITextScrubber scrubber, string text, int limit)
    {
        var scrubbed = scrubber.Scrub(text);
        return scrubbed.Length <= limit ? scrubbed : string.Concat(scrubbed.AsSpan(0, limit - 1), "…");
    }
}
