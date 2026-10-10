namespace Aw.Readiness.Core;

/// <summary>The level a ring must hold (readiness.yaml, spec §4e B11).</summary>
/// <param name="Ring">The ring (0 is this toolkit and the owner's own repos).</param>
/// <param name="Level">The level repos in that ring must reach.</param>
public sealed record ReadinessTarget(int Ring, int Level);
