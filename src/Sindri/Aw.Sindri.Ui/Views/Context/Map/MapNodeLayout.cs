using Avalonia;

namespace Aw.Sindri.Ui.Views.Context.Map;

/// <summary>One node placed on the map's world: where it sits and the served facts the view draws from.</summary>
/// <param name="Key">The node's key as served ("feature:…", "decision:…", "code:path").</param>
/// <param name="Label">The served label (a file's name for a code node).</param>
/// <param name="Kind">The served kind word, or "ticket", "decision" or "file" for the records, decisions and files.</param>
/// <param name="Band">The band the node sits in.</param>
/// <param name="Shape">Chip or card.</param>
/// <param name="Bounds">The node's rectangle on the world.</param>
/// <param name="IsAnchor">Whether this is a starting point of the read.</param>
/// <param name="Confirmed">Whether a person confirmed it.</param>
/// <param name="Provenance">How it came to be (human, deterministic, model).</param>
/// <param name="Confidence">How sure the tool is, 0 to 1.</param>
/// <param name="Detail">The technical detail for the caption (a source, a date, a directory), or empty.</param>
/// <param name="Path">The file's full path for a code node, else empty.</param>
/// <param name="IsTest">Whether a code node is a test file.</param>
/// <param name="HasConflict">Whether a decision node is part of a conflict.</param>
/// <param name="LinkId">The link a suggested code file waits on, or null.</param>
internal sealed record MapNodeLayout(
    string Key,
    string Label,
    string Kind,
    MapBand Band,
    MapShape Shape,
    Rect Bounds,
    bool IsAnchor,
    bool Confirmed,
    string Provenance,
    double Confidence,
    string Detail,
    string Path,
    bool IsTest,
    bool HasConflict,
    string? LinkId)
{
    /// <summary>Gets the left edge, px (plain numbers for the view models, which carry no Avalonia types).</summary>
    internal double X => Bounds.X;

    /// <summary>Gets the top edge, px.</summary>
    internal double Y => Bounds.Y;

    /// <summary>Gets the width, px.</summary>
    internal double Width => Bounds.Width;

    /// <summary>Gets the height, px.</summary>
    internal double Height => Bounds.Height;

    /// <summary>Gets a value indicating whether the node is a suggestion that nobody confirmed (drawn dashed).</summary>
    internal bool IsSuggested => !Confirmed && string.Equals(Provenance, "model", StringComparison.Ordinal);
}
