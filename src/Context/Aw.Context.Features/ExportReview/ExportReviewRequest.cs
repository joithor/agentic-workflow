using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ExportReview;

/// <summary>Writes the queue as a review file under the exports folder.</summary>
internal sealed record ExportReviewRequest(string? Path, ContextScope Scope);
