using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Context.Features.ApplyReviewFile;
using Aw.Context.Features.ExportReview;
using Aw.Context.Features.PreviewReviewFile;
using Aw.Context.Features.PromoteMap;
using Aw.Context.Features.ReviewHistory;
using Aw.Context.Features.ReviewMap;
using Aw.Context.Features.ReviewQueue;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The review half of the map facade (S-4): the queue, one decision, a review file and the promote.</summary>
internal sealed class MapReview(
    IService<ReviewQueueRequest, ReviewQueueView> queue,
    IService<ReviewMapRequest, ReviewResult> review,
    IService<ReviewHistoryRequest, ReviewHistoryView> history,
    IService<ExportReviewRequest, ReviewExported> exportReview,
    IService<PreviewReviewFileRequest, ReviewFilePreview> previewFile,
    IService<ApplyReviewFileRequest, ReviewResult> applyFile,
    IService<PromoteMapRequest, MapPromoted> promote)
{
    public ValueTask<Result<ReviewQueueView>> ReviewQueue(int? limit, ContextScope scope, CancellationToken cancellationToken) =>
        queue.Handle(new ReviewQueueRequest(scope, limit), cancellationToken);

    public ValueTask<Result<ReviewResult>> ApplyReview(ReviewDecision decision, ContextScope scope, CancellationToken cancellationToken) =>
        review.Handle(new ReviewMapRequest(decision, scope), cancellationToken);

    public ValueTask<Result<ReviewHistoryView>> ReviewHistory(int? limit, ContextScope scope, CancellationToken cancellationToken) =>
        history.Handle(new ReviewHistoryRequest(scope, limit), cancellationToken);

    public ValueTask<Result<ReviewExported>> ExportReview(string? path, ContextScope scope, CancellationToken cancellationToken) =>
        exportReview.Handle(new ExportReviewRequest(path, scope), cancellationToken);

    public ValueTask<Result<ReviewFilePreview>> PreviewReviewFile(string path, ContextScope scope, CancellationToken cancellationToken) =>
        previewFile.Handle(new PreviewReviewFileRequest(path, scope), cancellationToken);

    public ValueTask<Result<ReviewResult>> ApplyReviewFile(string path, ContextScope scope, CancellationToken cancellationToken) =>
        applyFile.Handle(new ApplyReviewFileRequest(path, scope), cancellationToken);

    public ValueTask<Result<MapPromoted>> Promote(ContextScope scope, CancellationToken cancellationToken, int? expectedVersion = null) =>
        promote.Handle(new PromoteMapRequest(scope, expectedVersion), cancellationToken);
}
