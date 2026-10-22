using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The human's list (API spec §3.1, §3.6): the 'needs_you' rows the caller may see (a session its repo's and the repo-less ones) in
/// <see cref="NeedsYouOrder"/>. With no row the sentence counts the running and queued items the caller sees; stale views replace it.
/// </summary>
/// <param name="views">The 'needs_you' and 'items' views.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller, whose repo scope applies.</param>
/// <param name="onyx">The managed Onyx's notice (not set up, or serve could not start it); it is a repo-less row, shown to a human caller only.</param>
/// <param name="setup">The setup rows that are computed (the repo step's failure, and the approve-all plan's 'setup:recipes' and 'recipe:' rows).</param>
/// <param name="scrubber">Scrubs the notice's and the rows' text.</param>
/// <param name="clock">Stamps the notice's row.</param>
internal sealed class GetNeedsYouService(IViewReader views, IProjectorStatus status, ICallerContextAccessor callers, IOnyxNotices onyx, SetupNotices setup, ITextScrubber scrubber, TimeProvider clock) : IService<GetNeedsYouRequest, GetNeedsYouResponse>
{
    public async ValueTask<Result<GetNeedsYouResponse>> Handle(GetNeedsYouRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("the needs-you list needs a caller");
        }

        var current = status.Current;
        var records = await views.List<NeedsYouRowRecord>(ViewNames.NeedsYou, cancellationToken).ConfigureAwait(false);
        if (records.IsError)
        {
            return records.Error;
        }

        var rows = NeedsYouOrder.Sort([.. records.Value.Select(static r => r.Row).Where(r => ItemScope.VisibleOrRepoless(caller, r.Repo)), .. await OnyxRows(caller, cancellationToken).ConfigureAwait(false), .. await SetupRows(caller, cancellationToken).ConfigureAwait(false)]);
        var empty = current.Text;
        if (empty is null && rows.Count == 0)
        {
            var counted = await Counts(caller, cancellationToken).ConfigureAwait(false);
            if (counted.IsError)
            {
                return counted.Error;
            }

            empty = EmptyText.NeedsYou(counted.Value.Running, counted.Value.Queued);
        }

        return new GetNeedsYouResponse(rows, empty, current.AsOf);
    }

    // The setup rows that are computed, not folded: the human's to act on, so a session doesn't see them.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> SetupRows(CallerContext caller, CancellationToken cancellationToken)
    {
        if (caller.Class != CallerClass.Human)
        {
            return [];
        }

        var repo = await setup.Repo(cancellationToken).ConfigureAwait(false);
        var recipes = await setup.Recipes(cancellationToken).ConfigureAwait(false);
        return repo is { } row ? [row, .. recipes] : recipes;
    }

    // The managed Onyx's row: it is the human's to act on, so a session doesn't see it.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> OnyxRows(CallerContext caller, CancellationToken cancellationToken)
    {
        if (caller.Class != CallerClass.Human)
        {
            return [];
        }

        var notice = await onyx.Current(cancellationToken).ConfigureAwait(false);
        if (notice is not { IsSuccess: true, Value: { } found })
        {
            return [];
        }

        var row = NeedsYouRows.ForOnyx(found, clock.GetUtcNow(), scrubber);
        return row.IsSuccess ? [row.Value] : [];
    }

    private async ValueTask<Result<(int Running, int Queued)>> Counts(CallerContext caller, CancellationToken cancellationToken)
    {
        var items = await views.List<ItemRow>(ViewNames.Items, cancellationToken).ConfigureAwait(false);
        if (items.IsError)
        {
            return items.Error;
        }

        var seen = items.Value.Where(r => ItemScope.Visible(caller, r.Repo)).Select(static r => QueueGroups.GroupOf(r.State)).ToList();
        return (seen.Count(static g => g == QueueGroup.Running), seen.Count(static g => g == QueueGroup.Queued));
    }
}
