using Aw.Context.Core.Engine;

namespace Aw.Context.Features.HeldMap;

/// <summary>Reads what the publish rules need: the hold, the latest draft, the current version and its heads.</summary>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record HeldMapRequest(ContextScope Scope);
