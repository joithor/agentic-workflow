using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ShowPublishStatus;

/// <summary>Reads the publish state as the caller may see it.</summary>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record ShowPublishStatusRequest(ContextScope Scope);
