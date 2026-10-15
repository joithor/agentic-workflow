using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;
/// <summary>The override log, newest first, through the engine.</summary>
internal sealed class GetContextReviewHistoryService : IService<GetContextReviewHistoryRequest, ReviewHistoryView>
{
    public ValueTask<Result<ReviewHistoryView>> Handle(GetContextReviewHistoryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
