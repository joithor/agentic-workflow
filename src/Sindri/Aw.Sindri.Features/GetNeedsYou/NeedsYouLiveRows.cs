using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.ApproveAll;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>The human's rows that are read at request time and not folded from events: the managed Onyx's notice and the approve-all plan's rows. Both are the human's to act on, so a session gets none.</summary>
/// <param name="onyx">The managed Onyx's notice (not set up, or serve could not start it); it is a repo-less row, shown to a human caller only.</param>
/// <param name="plans">The approve-all plan, which the 'setup:recipes' and 'recipe:' rows are read from; a plan that can't be read drops those rows only.</param>
/// <param name="scrubber">Scrubs the rows' text.</param>
/// <param name="clock">Stamps the rows.</param>
internal sealed class NeedsYouLiveRows(IOnyxNotices onyx, ApproveAllPlanReader plans, ITextScrubber scrubber, TimeProvider clock)
{
    /// <summary>The Onyx row and the recipe rows for <paramref name="caller"/>, in that order.</summary>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The rows; empty for a session or when a source can't be read.</returns>
    public async ValueTask<IReadOnlyList<NeedsYouRow>> Read(CallerContext caller, CancellationToken cancellationToken) =>
        [.. await OnyxRows(caller, cancellationToken).ConfigureAwait(false), .. await RecipeRows(caller, cancellationToken).ConfigureAwait(false)];

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

    // The approve-all plan's rows (setup-once spec §7.3): read at request time from the registry and the approvals, the human's to act on, and absent when the plan can't be read
    // (no approved profile yet, or the read failed): the list never fails for them.
    private async ValueTask<IReadOnlyList<NeedsYouRow>> RecipeRows(CallerContext caller, CancellationToken cancellationToken)
    {
        if (caller.Class != CallerClass.Human)
        {
            return [];
        }

        try
        {
            var plan = await plans.Read(cancellationToken).ConfigureAwait(false);
            if (plan.IsError)
            {
                return [];
            }

            var rows = NeedsYouRows.ForRecipes(plan.Value, clock.GetUtcNow(), scrubber);
            return rows.IsSuccess ? rows.Value : [];
        }
#pragma warning disable CA1031 // boundary: a registry that throws drops these rows, never the list
        catch (Exception e) when (e is not OperationCanceledException)
#pragma warning restore CA1031
        {
            return [];
        }
    }
}
