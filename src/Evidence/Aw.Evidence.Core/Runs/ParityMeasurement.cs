using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Images;

namespace Aw.Evidence.Core.Runs;

/// <summary>What a parity frame measured, before it becomes a <see cref="ParityFrame"/>.</summary>
/// <param name="Frame">The manifest frame.</param>
/// <param name="AnchorBox">The anchor's measured box.</param>
/// <param name="Comparison">The design-vs-implementation comparison.</param>
/// <param name="BoxChecks">The geometry checks.</param>
/// <param name="Files">Where its images were written.</param>
public sealed record ParityMeasurement(DesignFrame Frame, Box AnchorBox, PixelComparison Comparison, IReadOnlyList<BoxCheck> BoxChecks, ParityFiles Files)
{
    /// <summary>The frame's parity.json entry.</summary>
    /// <param name="manifest">The manifest (threshold and anti-aliasing).</param>
    /// <returns>The entry.</returns>
    public ParityFrame ToFrame(DesignManifest manifest)
    {
        var offset = Frame.AnchorOffset ?? Offset.Zero;
        var origin = ParityRegions.Implementation(AnchorBox, Frame.DesignRegion, offset);
        return new ParityFrame(
            Frame.Name,
            Frame.DesignRegion,
            new Offset(origin.X, origin.Y),
            AnchorBox,
            offset,
            new SizeDelta(Round2(AnchorBox.W - Frame.DesignRegion.W), Round2(AnchorBox.H - Frame.DesignRegion.H)),
            Comparison.DiffPixels,
            Comparison.Total,
            Comparison.DiffPercent,
            manifest.Threshold,
            manifest.IncludeAA,
            BoxChecks,
            Frame.KnownDifferences,
            Files,
            Frame.DesignNode);
    }

    private static double Round2(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
