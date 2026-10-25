namespace Aw.Sindri.Ui.Api.Context;

/// <summary>A node that lacks a lens; Missing names which.</summary>
/// <param name="Node">Node, as served.</param>
/// <param name="Missing">Missing, as served.</param>
/// <param name="Note">Note, as served.</param>
internal sealed record CoverageGap(
    string Node,
    IReadOnlyList<string> Missing,
    string? Note);
