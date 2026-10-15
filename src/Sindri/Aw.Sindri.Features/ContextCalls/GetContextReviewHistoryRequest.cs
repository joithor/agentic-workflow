using Aw.Context.Core.Review;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The override log, newest first (GET /sindri/context/map/review/history).</summary>
public sealed record GetContextReviewHistoryRequest() : IQuery<ReviewHistoryView>;
