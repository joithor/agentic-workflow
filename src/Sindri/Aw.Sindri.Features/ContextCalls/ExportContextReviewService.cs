using Aw.Context.Core.Facade;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Writes the review queue as a review file under the exports folder, through the engine.</summary>
internal sealed class ExportContextReviewService(ContextScopes scopes, IContextEngine engine) : IService<ExportContextReviewRequest, ReviewExported>
{
    public async ValueTask<Result<ReviewExported>> Handle(ExportContextReviewRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.ExportReview(request.Path, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
