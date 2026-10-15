using Aw.Context.Core.Map;

namespace Aw.Context.Core.Proposals;

/// <summary>What proposing wrote; 'current' is never touched.</summary>
/// <param name="Draft">The draft the rows went to (opened as a copy of current when there was none).</param>
/// <param name="Added">New 'propose' rows.</param>
/// <param name="Existing">Surfaces already in the map or already proposed (idempotent: no row).</param>
/// <param name="Rejected">Surfaces a person rejected earlier (never re-proposed: no row).</param>
public sealed record ProposeResult(MapVersion Draft, int Added, int Existing, int Rejected);
