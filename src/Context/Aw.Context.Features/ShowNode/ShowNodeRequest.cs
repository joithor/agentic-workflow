using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ShowNode;

/// <summary>One node by key, alias or name.</summary>
/// <param name="Key">The key, alias or label.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record ShowNodeRequest(string Key, ContextScope Scope);
