namespace Aw.Context.Core.Lenses;

/// <summary>How far a lenses call reaches: the depth and the cascade hops a transport sent (both text-checked by the call).</summary>
/// <param name="Depth">How far the neighbors reach and the cascade runs (0 to 4), or null for the profile's 'context.cascadeDepth'.</param>
/// <param name="Hops">A comma list of state, flow, dependency and component, or null for the first three.</param>
public sealed record LensRange(int? Depth, string? Hops = null);
