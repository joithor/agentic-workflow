using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.Graph;

/// <summary>The map a caller may read: the version, and its graph with overrides applied and a session's scope cut.</summary>
/// <param name="Snapshot">The version as stored.</param>
/// <param name="Graph">The graph the caller sees.</param>
internal sealed record VisibleMap(MapSnapshot Snapshot, GraphSnapshot Graph);
