using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// The span processor in front of the export processor (installed only when a trace exporter is): scrubs a span before
/// the exporter sees it. The display name, tag values (a tag with a secret's name is withheld whole, whatever its type;
/// a list is scrubbed element by element) and the status description are rewritten in place. The operation name,
/// trace state, tag keys, events and links can't be rewritten, so a span with a secret in any of them, or whose scrub
/// was blocked or failed, is dropped: it is marked not recorded, which the export processor skips, and counted.
/// </summary>
internal sealed class ScrubbingActivityProcessor(Scrubber scrubber, Withheld withheld) : BaseProcessor<Activity>
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Telemetry must never throw into the code that ended the span; a span that can't be scrubbed is dropped and counted.")]
    public override void OnEnd(Activity data)
    {
        try
        {
            if (Scrub(data))
            {
                return;
            }

            withheld.Span();
        }
        catch (Exception)
        {
            withheld.Failure();
        }

        data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }

    // True when the span may be exported (rewritten in place where needed). What can't be rewritten (the operation name,
    // trace state, tag keys, events and links) goes into its own tally: anything found there drops the span.
    private bool Scrub(Activity data)
    {
        var tally = default(ScrubTally);
        var unwritable = default(ScrubTally);
        _ = scrubber.Scrub(data.OperationName, ref unwritable);
        if (data.TraceStateString is { } traceState)
        {
            _ = scrubber.Scrub(traceState, ref unwritable);
        }

        data.DisplayName = scrubber.Scrub(data.DisplayName, ref tally);
        ScrubTags(data, ref tally, ref unwritable);
        if (data.StatusDescription is { } description)
        {
            _ = data.SetStatus(data.Status, scrubber.Scrub(description, ref tally));
        }

        foreach (var activityEvent in data.Events)
        {
            _ = scrubber.Scrub(activityEvent.Name, ref unwritable);
            ScrubAll(activityEvent.Tags, ref unwritable);
        }

        foreach (var link in data.Links)
        {
            ScrubAll(link.Tags, ref unwritable);
        }

        return !tally.Blocked && unwritable.Clean;
    }

    private void ScrubTags(Activity data, ref ScrubTally tally, ref ScrubTally unwritable)
    {
        foreach (var (key, value) in data.TagObjects.ToList())
        {
            _ = scrubber.Scrub(key, ref unwritable);

            // A fresh tally per tag: the record's tally may already say "redacted" from the name.
            var tag = default(ScrubTally);
            var scrubbed = scrubber.ScrubValue(key, value, ref tag);
            if (!tag.Clean)
            {
                _ = data.SetTag(key, scrubbed);
                tally = tally with { Redacted = tally.Redacted || tag.Redacted, Blocked = tally.Blocked || tag.Blocked };
            }
        }
    }

    private void ScrubAll(IEnumerable<KeyValuePair<string, object?>>? tags, ref ScrubTally tally)
    {
        foreach (var (key, value) in tags ?? [])
        {
            _ = scrubber.Scrub(key, ref tally);
            _ = scrubber.ScrubValue(key, value, ref tally);
        }
    }
}
