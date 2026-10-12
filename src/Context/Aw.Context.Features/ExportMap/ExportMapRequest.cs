using Aw.Context.Core.Map;

namespace Aw.Context.Features.ExportMap;

/// <summary>Writes the current version (with its overrides) to the exports folder.</summary>
/// <param name="Path">A name or path under the exports folder, or null.</param>
/// <param name="Reader">The caller and settings.</param>
internal sealed record ExportMapRequest(string? Path, MapReader Reader);
