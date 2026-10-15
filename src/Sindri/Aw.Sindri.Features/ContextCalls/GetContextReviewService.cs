using Aw.Context.Core.Facade;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What is left to decide in the latest draft, through the engine (which refuses sessions: 020).</summary>
internal sealed class GetContextReviewService(ContextScopes scopes, IContextEngine engine) : IService<GetContextReviewRequest, ReviewQueueView>
{
    public async ValueTask<Result<ReviewQueueView>> Handle(GetContextReviewRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.ReviewQueue(scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
