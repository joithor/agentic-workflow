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

        var overrides = await store.Overrides(cancellationToken).ConfigureAwait(false);
        return overrides.IsError ? overrides.Error : Core.Review.ReviewHistory.Of(overrides.Value);
    }
}
