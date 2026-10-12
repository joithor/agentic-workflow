using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ShowMapStatus;

/// <summary>The current version and its counts.</summary>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record ShowMapStatusRequest(ContextScope Scope);
