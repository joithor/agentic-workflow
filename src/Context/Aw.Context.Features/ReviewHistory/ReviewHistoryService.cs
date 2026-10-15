using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ReviewHistory;

/// <summary>The override log of the map, newest first, with what each undo cancelled (spec §8.7 'map review --history'). A session is refused like the rest of the review (SND-CONTEXT-020).</summary>
internal sealed class ReviewHistoryService(IGraphStore store) : IService<ReviewHistoryRequest, ReviewHistoryView>
{
    public async ValueTask<Result<ReviewHistoryView>> Handle(ReviewHistoryRequest request, CancellationToken cancellationToken)
    {
        if (ReviewDrafts.Gate(request.Scope, "reading the review history") is { } refused)
        {
            return refused;
        }

        var limit = ReviewLimits.Resolve(request.Limit, Core.Review.ReviewHistory.DefaultLimit);
        var overrides = limit.IsError ? limit.Error : await store.Overrides(cancellationToken).ConfigureAwait(false);
        return limit.IsError || overrides.IsError ? (limit.IsError ? limit.Error : overrides.Error) : Core.Review.ReviewHistory.Of(overrides.Value, limit.Value);
    }
}
