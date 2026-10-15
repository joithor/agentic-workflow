using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.StreamEvents;

/// <summary>
/// Reads events after the cursor from the ledger. It reads on, page by page, until it has <see cref="StreamEventsRequest.Max"/>
/// events a caller may see (S1-D6 filters, then <see cref="PublicEvents"/> for what a caller sees of a row, then
/// <see cref="EventVisibility"/> for a session's repo), or reaches the head, and reports how far it read. An item's repo is read
/// from the ledger, so the scope doesn't depend on the views being current; when it can't be read the call fails and the
/// cursor stays where it was.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="scrubber">Masks secrets in every string a payload sends.</param>
/// <param name="callers">The caller, whose repo scope applies when it is a session.</param>
/// <param name="repos">Where each item belongs, from the ledger.</param>
/// <param name="json">The package's AwJson options, for the public payloads.</param>
internal sealed class StreamEventsService(ILedgerReader ledger, ITextScrubber scrubber, ICallerContextAccessor callers, IItemRepos repos, JsonSerializerOptions json)
    : IService<StreamEventsRequest, StreamEventsResponse>
{
    public async ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var max = Math.Clamp(request.Max, 1, 500);
        var cursor = Math.Max(request.After, 0);
        var kept = new List<PublicEvent>();
        while (kept.Count < max)
        {
            var page = await ledger.ReadAfter(cursor, max, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var row in page.Value)
            {
                var shown = await Shown(request, row, cancellationToken).ConfigureAwait(false);
                if (shown.IsError)
                {
                    // the cursor stays at the last row dealt with, never past a row this caller's scope couldn't be decided for
                    return kept.Count > 0 ? new StreamEventsResponse(kept, cursor) : shown.Error;
                }

                cursor = row.Sequence;
                if (shown.Value is { } view)
                {
                    kept.Add(view);
                    if (kept.Count == max)
                    {
                        break;
                    }
                }
            }

            if (page.Value.Count < max)
            {
                break;
            }
        }

        return new StreamEventsResponse(kept, cursor);
    }

    /// <summary>The row as this caller may see it, or null when it may not.</summary>
    private async ValueTask<Result<PublicEvent?>> Shown(StreamEventsRequest request, LedgerEvent row, CancellationToken cancellationToken)
    {
        if (!Keeps(request, row))
        {
            return (PublicEvent?)null;
        }

        var view = PublicEvents.For(row, json, scrubber, includeNonPublic: false);
        if (view.IsError || view.Value is null)
        {
            return view;
        }

        if (callers.Current is { } caller)
        {
            var visible = await EventVisibility.Visible(caller, row, repos, cancellationToken).ConfigureAwait(false);
            if (visible.IsError)
            {
                return visible.Error;
            }

            if (!visible.Value)
            {
                return (PublicEvent?)null;
            }
        }

        return view;
    }

    private static bool Keeps(StreamEventsRequest request, LedgerEvent e) =>
        (request.Item is not { } item || string.Equals(e.Item, item.Value, StringComparison.Ordinal))
        && (request.Instance is not { } instance || InstanceEvents.Belongs(e, instance));
}
