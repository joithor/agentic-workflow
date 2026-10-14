using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ListNeighbors;

/// <summary>The nodes near one node.</summary>
/// <param name="Key">The key, alias or label.</param>
/// <param name="Depth">0 to 4, or null for 1.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record ListNeighborsRequest(string Key, int? Depth, ContextScope Scope);
