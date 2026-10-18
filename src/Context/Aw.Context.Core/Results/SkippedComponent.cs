namespace Aw.Context.Core.Results;

/// <summary>A code unit the component hop did not walk because it implements more features than the cap (a shared utility would relate everything).</summary>
/// <param name="Unit">The code unit's key, scrubbed like every code path.</param>
/// <param name="Features">How many features it implements in the graph the caller can see.</param>
public sealed record SkippedComponent(string Unit, int Features);
