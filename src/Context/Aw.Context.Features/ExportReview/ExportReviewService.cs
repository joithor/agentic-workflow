using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ExportReview;

/// <summary>Writes the queue as a review file under the exports folder. (S-4 stub: red until its slice is built.)</summary>
internal sealed class ExportReviewService : IService<ExportReviewRequest, ReviewExported>
{
    public ValueTask<Result<ReviewExported>> Handle(ExportReviewRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: ExportReview is not built yet");
}
