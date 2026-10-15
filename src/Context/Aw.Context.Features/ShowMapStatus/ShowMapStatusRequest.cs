using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ShowMapStatus;

/// <summary>The current version and its counts.</summary>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
/// <param name="Anchor">A node key, name or alias to report the coverage of, or null for none.</param>
internal sealed record ShowMapStatusRequest(ContextScope Scope, string? Anchor);
