using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ReviewHistory;

/// <summary>The override log of the map, newest first, with what each undo cancelled (spec §8.7 'map review --history').</summary>
internal sealed class ReviewHistoryService : IService<ReviewHistoryRequest, ReviewHistoryView>
{
    public ValueTask<Result<ReviewHistoryView>> Handle(ReviewHistoryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
