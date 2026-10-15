using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ApplyReviewFile;

/// <summary>
/// Applies an edited review file to the latest draft (spec §8.7 'map review --apply'): each decided entry that still sits in the
/// queue becomes override rows, the rest are listed as skipped; SND-CONTEXT-043 only when nothing matches. An entry the
/// draft no longer allows (it changed since the file was read) is skipped too. Sindri checks the caller first (protected).
/// </summary>
internal sealed class ApplyReviewFileService(IGraphStore store, IReviewFileStore files, ICurrentUser user, TimeProvider clock) : IService<ApplyReviewFileRequest, ReviewResult>
{
    public async ValueTask<Result<ReviewResult>> Handle(ApplyReviewFileRequest request, CancellationToken cancellationToken)
    {
        if (ReviewDrafts.Gate(request.Scope, "applying a review file") is { } refused)
        {
            return refused;
        }

        var file = await files.Read(request.Path, cancellationToken).ConfigureAwait(false);
        var draft = file.IsError ? file.Error : await ReviewDrafts.Draft(store, cancellationToken).ConfigureAwait(false);
        if (file.IsError || draft.IsError)
        {
            return file.IsError ? file.Error : draft.Error;
        }

        var map = draft.Value;
        var matched = ReviewFileMatcher.Match(file.Value, ReviewBacklog.Build(map.Label, map.Graph, map.Overrides));
        if (matched.IsError)
        {
            return matched.Error;
        }

        var (rows, skipped) = Plan(matched.Value, map, (ReviewDrafts.Who(request.Scope.Caller, user), clock.GetUtcNow()));
        var added = await store.AppendOverrides(rows, cancellationToken).ConfigureAwait(false);
        return added.IsError
            ? added.Error
            : new ReviewResult([.. rows.Select(r => r.ToId())], skipped, ReviewBacklog.Build(map.Label, map.Graph, [.. map.Overrides, .. rows]).Undecided);
    }

    // Each decision is planned on top of the ones before it, so two entries of one file can't both claim the same item.
    private static (List<MapOverride> Rows, List<string> Skipped) Plan(ReviewFileMatch matched, MapSnapshot map, (string Who, DateTimeOffset At) by)
    {
        var rows = new List<MapOverride>();
        var skipped = new List<string>(matched.Skipped);
        foreach (var decision in matched.Decisions)
        {
            var planned = ReviewPlanner.Plan(decision, map.Graph, [.. map.Overrides, .. rows], by.Who, by.At);
            if (planned.IsSuccess)
            {
                rows.AddRange(planned.Value);
            }
            else
            {
                skipped.Add(planned.Error.Message);
            }
        }

        return (rows, skipped);
    }
}
