using Aw.Context.Core.Engine;

namespace Aw.Context.Features.SourcesStatus;

/// <summary>Asks for the sources' state in a scope.</summary>
/// <param name="Scope">The profile's settings and repos, and the caller.</param>
internal sealed record SourcesStatusRequest(ContextScope Scope);
