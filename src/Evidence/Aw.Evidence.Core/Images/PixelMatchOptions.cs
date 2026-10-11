namespace Aw.Evidence.Core.Images;

/// <summary>How strict a pixel comparison is.</summary>
/// <param name="Threshold">Per-pixel colour tolerance, 0 (exact) to 1 (anything matches); 0.1 is the usual value.</param>
/// <param name="IncludeAA">Count pixels that look anti-aliased as differences.</param>
public sealed record PixelMatchOptions(double Threshold, bool IncludeAA)
{
    /// <summary>Threshold 0.1, anti-aliasing ignored.</summary>
    public static PixelMatchOptions Default { get; } = new(0.1, IncludeAA: false);
}
