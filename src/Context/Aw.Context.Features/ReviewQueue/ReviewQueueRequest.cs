using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ReviewQueue;

/// <summary>What is left to decide in the latest draft.</summary>
internal sealed record ReviewQueueRequest(ContextScope Scope, int? Limit);
