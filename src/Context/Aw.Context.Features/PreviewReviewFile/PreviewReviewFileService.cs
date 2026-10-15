using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.PreviewReviewFile;

/// <summary>Reads a review file and answers its hash and counts, writing nothing (spec §8.7 'map review --apply' shows this before the typed confirmation).</summary>
internal sealed class PreviewReviewFileService : IService<PreviewReviewFileRequest, ReviewFilePreview>
{
    public ValueTask<Result<ReviewFilePreview>> Handle(PreviewReviewFileRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
