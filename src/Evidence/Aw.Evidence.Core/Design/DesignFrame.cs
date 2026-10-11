using System.Text.Json.Serialization;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Core.Design;

/// <summary>One design state to compare: the design export, the region in it, and how to find and prepare the matching page state.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesignFrame
{
    /// <summary>Creates a frame; JSON reads through this constructor so missing lists are empty.</summary>
    /// <param name="name">File-name safe name.</param>
    /// <param name="designPng">The design export.</param>
    /// <param name="designRegion">The compared region of the export.</param>
    /// <param name="anchor">The element locating the implementation crop.</param>
    /// <param name="anchorOffset">Added to the anchor's origin.</param>
    /// <param name="waitFor">Visible before the steps run.</param>
    /// <param name="steps">Steps that bring the page into this state.</param>
    /// <param name="formValues">Values typed into the form.</param>
    /// <param name="expectBoxes">Geometry assertions.</param>
    /// <param name="knownDifferences">Intentional differences.</param>
    /// <param name="designNode">The design node the frame was exported from.</param>
    [JsonConstructor]
    public DesignFrame(
        string name,
        string designPng,
        Region designRegion,
        ElementSelector anchor,
        Offset? anchorOffset = null,
        ElementSelector? waitFor = null,
        IReadOnlyList<ScriptStep>? steps = null,
        IReadOnlyList<FormValue>? formValues = null,
        IReadOnlyList<BoxExpectation>? expectBoxes = null,
        IReadOnlyList<string>? knownDifferences = null,
        string? designNode = null)
    {
        Name = name;
        DesignPng = designPng;
        DesignRegion = designRegion;
        Anchor = anchor;
        AnchorOffset = anchorOffset;
        WaitFor = waitFor;
        Steps = steps ?? [];
        FormValues = formValues ?? [];
        ExpectBoxes = expectBoxes ?? [];
        KnownDifferences = knownDifferences ?? [];
        DesignNode = designNode;
    }

    /// <summary>File-name safe name (letters, digits, '_' and '-', starting with a letter or digit); names the output files.</summary>
    public string Name { get; init; }

    /// <summary>The design export (PNG at 1x).</summary>
    public string DesignPng { get; init; }

    /// <summary>The compared region of the design export.</summary>
    public Region DesignRegion { get; init; }

    /// <summary>The element whose bounding-box origin locates the implementation crop.</summary>
    public ElementSelector Anchor { get; init; }

    /// <summary>Added to the anchor's origin; null is no offset.</summary>
    public Offset? AnchorOffset { get; init; }

    /// <summary>Wait for this to be visible before the steps run.</summary>
    public ElementSelector? WaitFor { get; init; }

    /// <summary>Steps that bring the page into this state.</summary>
    public IReadOnlyList<ScriptStep> Steps { get; init; }

    /// <summary>Values typed into the form so the page matches the design's sample data.</summary>
    public IReadOnlyList<FormValue> FormValues { get; init; }

    /// <summary>Geometry assertions checked on this state.</summary>
    public IReadOnlyList<BoxExpectation> ExpectBoxes { get; init; }

    /// <summary>Intentional differences that explain a non-zero diff.</summary>
    public IReadOnlyList<string> KnownDifferences { get; init; }

    /// <summary>The design node (id or URL) this frame was exported from.</summary>
    public string? DesignNode { get; init; }
}
