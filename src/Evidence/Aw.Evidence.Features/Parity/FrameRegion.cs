using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;

namespace Aw.Evidence.Features.Parity;

/// <summary>A frame with its measured anchor and the implementation region to crop.</summary>
/// <param name="Frame">The frame.</param>
/// <param name="Anchor">The anchor's bounding box.</param>
/// <param name="Region">The implementation region (the design region's size at the anchor plus its offset).</param>
internal sealed record FrameRegion(DesignFrame Frame, Box Anchor, Region Region);
