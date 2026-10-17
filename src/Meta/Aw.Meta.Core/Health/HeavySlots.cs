namespace Aw.Meta.Core.Health;

/// <summary>The box-wide heavy slots and how many of them the meta gates reserve.</summary>
/// <param name="Cap">The box-wide slot count.</param>
/// <param name="Reserve">The slots reserved for the meta gates (0 to the cap).</param>
public sealed record HeavySlots(int Cap, int Reserve);
