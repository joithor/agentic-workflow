using Aw.Meta.Core.Lanes;

namespace Aw.Meta.Core.Coordination;

/// <summary>The lane map after a refresh.</summary>
/// <param name="Map">The map.</param>
/// <param name="Changed">Whether the refresh changed the assignments (and so the version).</param>
public sealed record LaneRefresh(LaneMapState Map, bool Changed);
