using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ExportMap;

/// <summary>Writes the current version (with its overrides) to the exports folder.</summary>
/// <param name="Path">A name or path under the exports folder, or null.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record ExportMapRequest(string? Path, ContextScope Scope);
