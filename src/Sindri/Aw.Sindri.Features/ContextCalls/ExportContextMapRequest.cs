using Aw.Context.Core.Map;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>GET /sindri/context/map/export: writes the current map to $AW_SINDRI_HOME/context/exports/ (context spec Phase 3).</summary>
/// <param name="Path">A name or path under the exports folder, or null for 'map-v&lt;N&gt;-&lt;time&gt;.json'.</param>
public sealed record ExportContextMapRequest(string? Path) : IQuery<MapExported>;
