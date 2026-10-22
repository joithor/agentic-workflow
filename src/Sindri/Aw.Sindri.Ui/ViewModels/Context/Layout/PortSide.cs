namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>The side of a node's box an edge's port sits on; the edge leaves or arrives along that side's outward normal.</summary>
internal enum PortSide
{
    /// <summary>The top edge: the normal points up.</summary>
    Top,

    /// <summary>The bottom edge: the normal points down.</summary>
    Bottom,

    /// <summary>The left edge: the normal points left.</summary>
    Left,

    /// <summary>The right edge: the normal points right.</summary>
    Right,
}
