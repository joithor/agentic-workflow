using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ReviewQueue;

/// <summary>What is left to decide in the latest draft. (S-4 stub: red until its slice is built.)</summary>
internal sealed class ReviewQueueService : IService<ReviewQueueRequest, ReviewQueueView>
{
    public ValueTask<Result<ReviewQueueView>> Handle(ReviewQueueRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: ReviewQueue is not built yet");
}
