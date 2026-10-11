namespace Aw.Evidence.Core.Runs;

/// <summary>Anchor size minus design region size; non-zero means the layout itself differs.</summary>
/// <param name="W">Width difference.</param>
/// <param name="H">Height difference.</param>
public sealed record SizeDelta(double W, double H);
