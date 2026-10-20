using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Runs;

namespace Aw.Evidence.Testing;

/// <summary>Builds a <see cref="ParitySummary"/> under /runs/parity-1.</summary>
public sealed class ParitySummaryBuilder
{
    private readonly List<ParityFrame> _frames = [];
    private Provenance _provenance = Provenance.Seeded;
    private string? _appBuild;
    private string? _designNode;

    /// <summary>A frame with <paramref name="diffPixels"/> of 100 pixels differing.</summary>
    /// <param name="name">The name.</param>
    /// <param name="diffPixels">Differing pixels.</param>
    /// <param name="checks">Its geometry checks.</param>
    /// <param name="known">Its known differences.</param>
    /// <returns>The frame.</returns>
    public static ParityFrame Frame(string name = "default", long diffPixels = 3, IReadOnlyList<BoxCheck>? checks = null, IReadOnlyList<string>? known = null) =>
        new(name, new Region(0, 0, 10, 10), new Offset(5, 5), new Box(5, 5, 10, 10), Offset.Zero, new SizeDelta(0, 0),
            diffPixels, 100, diffPixels, 0.1, IncludeAA: false, checks ?? [], known ?? [], ParityFiles.For("/run/parity-1", name), DesignNode: null);

    /// <summary>Adds a frame.</summary>
    /// <param name="frame">The frame.</param>
    /// <returns>This builder.</returns>
    public ParitySummaryBuilder WithFrame(ParityFrame frame)
    {
        _frames.Add(frame);
        return this;
    }

    /// <summary>Sets the provenance.</summary>
    /// <param name="provenance">The provenance.</param>
    /// <returns>This builder.</returns>
    public ParitySummaryBuilder WithProvenance(Provenance provenance)
    {
        _provenance = provenance;
        return this;
    }

    /// <summary>Sets the verified build and the design node.</summary>
    /// <param name="appBuild">The commit.</param>
    /// <param name="designNode">The design node.</param>
    /// <returns>This builder.</returns>
    public ParitySummaryBuilder Verified(string? appBuild, string? designNode)
    {
        (_appBuild, _designNode) = (appBuild, designNode);
        return this;
    }

    /// <summary>Builds the summary (one default frame when none were added).</summary>
    /// <returns>The summary.</returns>
    public ParitySummary Build() => new(ParitySummary.SchemaName, RunId.From("parity-1"), "http://localhost:3000", _provenance, _appBuild, _designNode,
        new ViewportSize(1512, 982), _frames.Count > 0 ? [.. _frames] : [Frame()]);
}
