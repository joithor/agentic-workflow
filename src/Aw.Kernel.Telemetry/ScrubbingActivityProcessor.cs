using System.Diagnostics;
using OpenTelemetry;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// The first span processor: scrubs a span before any exporter sees it. The name, tag values (a tag with a secret's
/// name is withheld whole) and the status description are rewritten in place. Events and links can't be rewritten,
/// so a span whose events or links carry a secret, or whose scrub was blocked, is dropped: it is marked not recorded,
/// which every export processor skips, and counted.
/// </summary>
internal sealed class ScrubbingActivityProcessor(Scrubber scrubber, Withheld withheld) : BaseProcessor<Activity>
{
    public override void OnEnd(Activity data)
    {
        var tally = default(ScrubTally);
        data.DisplayName = scrubber.Scrub(data.DisplayName, ref tally);
        ScrubTags(data, ref tally);
        if (data.StatusDescription is { } description)
        {
            _ = data.SetStatus(data.Status, scrubber.Scrub(description, ref tally));
        }

        var unwritable = default(ScrubTally);
        ScrubEventsAndLinks(data, ref unwritable);
        if (tally.Blocked || !unwritable.Clean)
        {
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            withheld.Span();
        }
    }

    private void ScrubTags(Activity data, ref ScrubTally tally)
    {
        foreach (var (key, value) in data.TagObjects.ToList())
        {
            if (value is null or bool or int or long or double)
            {
                continue;
            }

            // A fresh tally per tag: the record's tally may already say "redacted" from the name.
            var tag = default(ScrubTally);
            var text = scrubber.ScrubField(key, value, ref tag);
            if (!tag.Clean)
            {
                _ = data.SetTag(key, text);
                tally = tally with { Redacted = tally.Redacted || tag.Redacted, Blocked = tally.Blocked || tag.Blocked };
            }
        }
    }

    private void ScrubEventsAndLinks(Activity data, ref ScrubTally tally)
    {
        foreach (var activityEvent in data.Events)
        {
            _ = scrubber.Scrub(activityEvent.Name, ref tally);
            ScrubAll(activityEvent.Tags, ref tally);
        }

        foreach (var link in data.Links)
        {
            ScrubAll(link.Tags, ref tally);
        }
    }

    private void ScrubAll(IEnumerable<KeyValuePair<string, object?>>? tags, ref ScrubTally tally)
    {
        foreach (var (key, value) in tags ?? [])
        {
            _ = scrubber.ScrubField(key, value, ref tally);
        }
    }
}
