using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;
/// <summary>What is left to decide in the latest draft, through the engine (which refuses sessions: 020).</summary>
internal sealed class GetContextReviewService : IService<GetContextReviewRequest, ReviewQueueView>
{
    public ValueTask<Result<ReviewQueueView>> Handle(GetContextReviewRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
