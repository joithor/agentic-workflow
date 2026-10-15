using Aw.Context.Core.Facade;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The override log, newest first, through the engine.</summary>
internal sealed class GetContextReviewHistoryService(ContextScopes scopes, IContextEngine engine) : IService<GetContextReviewHistoryRequest, ReviewHistoryView>
{
    public async ValueTask<Result<ReviewHistoryView>> Handle(GetContextReviewHistoryRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.ReviewHistory(scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
