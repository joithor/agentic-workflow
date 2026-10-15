using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetItemTimeline;

/// <summary>
/// An item's timeline (API spec §3.1): the 'item-timeline' rows after the cursor, a page of 100. It reads 101 and says 'next' (the page's last
/// sequence) only when the 101st exists, so the last page always says null (U2-API-4).
/// </summary>
/// <param name="items">Resolves the item within the caller's repo scope.</param>
/// <param name="views">The 'item-timeline' view.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller, whose repo scope applies.</param>
internal sealed class GetItemTimelineService(IItemResolver items, IViewReader views, IProjectorStatus status, ICallerContextAccessor callers)
    : IService<GetItemTimelineRequest, TimelineResponse>
{
    private const int PageSize = 100;

    public async ValueTask<Result<TimelineResponse>> Handle(GetItemTimelineRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("a timeline needs a caller");
        }

        var current = status.Current;
        var resolved = await items.Resolve(request.Item, caller, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var item = resolved.Value.Id.Value;
        var after = ItemTimelineKeys.Of(item, Math.Max(request.After ?? 0, 0));
        var rows = await views.Range<TimelineRow>(ViewNames.ItemTimeline, after, beforeKey: ItemTimelineKeys.EndOf(item), PageSize + 1, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var more = rows.Value.Count > PageSize;
        var page = more ? [.. rows.Value.Take(PageSize)] : rows.Value;
        var empty = EmptyText.For(current, page.Count == 0 && request.After is null, EmptyText.Timeline);
        return new TimelineResponse(page, more ? page[^1].Sequence : null, empty, current.AsOf);
    }
}
