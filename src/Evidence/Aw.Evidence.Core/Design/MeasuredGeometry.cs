namespace Aw.Evidence.Core.Design;

/// <summary>An element's measured size and position (relative to another element when the check says so).</summary>
/// <param name="W">Width.</param>
/// <param name="H">Height.</param>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
public sealed record MeasuredGeometry(double W, double H, double X, double Y);
