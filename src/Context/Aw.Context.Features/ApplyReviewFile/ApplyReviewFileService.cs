using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ApplyReviewFile;

/// <summary>Applies a review file; entries that match nothing are listed as skipped. (S-4 stub: red until its slice is built.)</summary>
internal sealed class ApplyReviewFileService : IService<ApplyReviewFileRequest, ReviewResult>
{
    public ValueTask<Result<ReviewResult>> Handle(ApplyReviewFileRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: ApplyReviewFile is not built yet");
}
