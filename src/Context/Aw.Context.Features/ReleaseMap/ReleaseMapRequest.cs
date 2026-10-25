using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ReleaseMap;

/// <summary>Drops the hold on a draft.</summary>
/// <param name="Version">The held draft.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record ReleaseMapRequest(int Version, ContextScope Scope);
