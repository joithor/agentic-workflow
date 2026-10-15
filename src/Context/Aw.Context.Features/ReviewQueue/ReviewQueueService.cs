using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ReviewQueue;

/// <summary>What is left to decide in the latest draft: deterministic proposals, model ones and suggested links by impact, then orphaned overrides (spec §8.7).</summary>
internal sealed class ReviewQueueService(IGraphStore store, IScrubberFactory scrubbers) : IService<ReviewQueueRequest, ReviewQueueView>
{
    public async ValueTask<Result<ReviewQueueView>> Handle(ReviewQueueRequest request, CancellationToken cancellationToken) =>
        (await ReviewDrafts.Load(store, scrubbers, request.Scope, "reading the review queue", cancellationToken).ConfigureAwait(false)).Map(loaded => loaded.Queue);
}
