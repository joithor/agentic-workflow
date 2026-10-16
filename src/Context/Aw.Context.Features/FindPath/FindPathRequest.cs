using Aw.Context.Core.Engine;

namespace Aw.Context.Features.FindPath;

/// <summary>The shortest path between two nodes.</summary>
/// <param name="From">A key, alias or label.</param>
/// <param name="To">A key, alias or label.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record FindPathRequest(string From, string To, ContextScope Scope);
