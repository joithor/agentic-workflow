using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry;

namespace Aw.Kernel.Telemetry;

/// <summary>
/// The span processor in front of the export processor (installed only when a trace exporter is): scrubs a span before
/// the exporter sees it. The display name, tag values (a tag with a secret's name is withheld whole, whatever its type;
/// a list is scrubbed element by element) and the status description are rewritten in place. The operation name,
/// trace state, tag keys, a repeated tag key's values, the source's tags, events and links (their tags and trace
/// state) can't be rewritten, so a span with a secret in any of them, or whose scrub was blocked or failed, is
/// dropped: it is marked not recorded, which the export processor skips, and counted.
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
        ScrubOptional(data.TraceStateString, ref unwritable);
        ScrubAll(data.Source.Tags, ref unwritable);

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
            ScrubOptional(link.Context.TraceState, ref unwritable);
            ScrubAll(link.Tags, ref unwritable);
        }

        return !tally.Blocked && unwritable.Clean;
    }

    // SetTag rewrites only the first tag with a key, and AddTag allows repeats: a repeated key that needs rewriting
    // can't be rewritten in place, so it counts as unwritable.
    private void ScrubTags(Activity data, ref ScrubTally tally, ref ScrubTally unwritable)
    {
        var tags = data.TagObjects.ToList();
        var repeated = tags.GroupBy(t => t.Key, StringComparer.Ordinal).Where(g => g.Skip(1).Any()).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, value) in tags)
        {
            _ = scrubber.Scrub(key, ref unwritable);

            // A fresh tally per tag: the record's tally may already say "redacted" from the name.
            var tag = default(ScrubTally);
            var scrubbed = scrubber.ScrubValue(key, value, ref tag);
            if (tag.Clean)
            {
                continue;
            }

            if (repeated.Contains(key))
            {
                unwritable = unwritable with { Redacted = true, Blocked = unwritable.Blocked || tag.Blocked };
                continue;
            }

            _ = data.SetTag(key, scrubbed);
            tally = tally with { Redacted = tally.Redacted || tag.Redacted, Blocked = tally.Blocked || tag.Blocked };
        }
    }

    private void ScrubOptional(string? text, ref ScrubTally tally)
    {
        if (text is not null)
        {
            _ = scrubber.Scrub(text, ref tally);
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
