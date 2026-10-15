using Aw.Context.Core.Engine;
using Aw.Context.Core.Review;

namespace Aw.Context.Features.ReviewMap;

/// <summary>One review decision: one override row (accept-deterministic one per entry).</summary>
internal sealed record ReviewMapRequest(ReviewDecision Decision, ContextScope Scope);
