using Aw.Context.Core.Review;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Write the review queue as a review file under the exports folder (GET /sindri/context/map/review/export).</summary>
/// <param name="Path">A name or path under the exports folder, or null.</param>
public sealed record ExportContextReviewRequest(string? Path) : IQuery<ReviewExported>;
