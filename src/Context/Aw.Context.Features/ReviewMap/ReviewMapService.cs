using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ReviewMap;

/// <summary>One review decision: one override row (accept-deterministic one per entry). (S-4 stub: red until its slice is built.)</summary>
internal sealed class ReviewMapService : IService<ReviewMapRequest, ReviewResult>
{
    public ValueTask<Result<ReviewResult>> Handle(ReviewMapRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: ReviewMap is not built yet");
}
