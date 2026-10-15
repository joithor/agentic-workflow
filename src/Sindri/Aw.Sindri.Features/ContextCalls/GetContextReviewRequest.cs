using Aw.Context.Core.Review;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>What is left to decide in the latest draft (GET /sindri/context/map/review).</summary>
public sealed record GetContextReviewRequest() : IQuery<ReviewQueueView>;
