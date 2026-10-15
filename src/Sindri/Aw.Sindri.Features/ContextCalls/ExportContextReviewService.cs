using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;
/// <summary>Writes the review queue as a review file under the exports folder, through the engine.</summary>
internal sealed class ExportContextReviewService : IService<ExportContextReviewRequest, ReviewExported>
{
    public ValueTask<Result<ReviewExported>> Handle(ExportContextReviewRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
