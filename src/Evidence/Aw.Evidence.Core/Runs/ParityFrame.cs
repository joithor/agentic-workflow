using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Core.Runs;

/// <summary>One frame's parity result.</summary>
/// <param name="Name">The frame's name.</param>
/// <param name="DesignRegion">The compared design region.</param>
/// <param name="ImplementationOrigin">Where the implementation crop started.</param>
/// <param name="AnchorBox">The anchor's measured box.</param>
/// <param name="AnchorOffset">The anchor offset used.</param>
/// <param name="SizeDelta">Anchor size minus design region size.</param>
/// <param name="DiffPixels">Differing pixels.</param>
/// <param name="Total">Compared pixels.</param>
/// <param name="DiffPercent">Percentage differing, two decimals.</param>
/// <param name="Threshold">The colour threshold used.</param>
/// <param name="IncludeAA">Whether anti-aliased pixels counted.</param>
/// <param name="BoxChecks">The geometry checks.</param>
/// <param name="KnownDifferences">Intentional differences.</param>
/// <param name="Files">The frame's images.</param>
/// <param name="DesignNode">The frame's own design node.</param>
public sealed record ParityFrame(
    string Name,
    Region DesignRegion,
    Offset ImplementationOrigin,
    Box AnchorBox,
    Offset AnchorOffset,
    SizeDelta SizeDelta,
    long DiffPixels,
    long Total,
    double DiffPercent,
    double Threshold,
    bool IncludeAA,
    IReadOnlyList<BoxCheck> BoxChecks,
    IReadOnlyList<string> KnownDifferences,
    ParityFiles Files,
    string? DesignNode);
