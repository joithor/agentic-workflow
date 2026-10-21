using Aw.Sindri.Ui.Views.Context.Map;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One edge's label pill: the words, the point on the path it is centred on, and when it shows.</summary>
internal sealed class MapPillViewModel
{
    /// <summary>A pill from its edge.</summary>
    /// <param name="edge">The edge it labels.</param>
    internal MapPillViewModel(MapEdgeLayout edge)
    {
        Edge = edge;
        Always = edge.CrossesBands || string.Equals(edge.Type, "conflicts", StringComparison.Ordinal);
    }

    /// <summary>The edge it labels.</summary>
    internal MapEdgeLayout Edge { get; }

    /// <summary>The edge's key.</summary>
    public string Key => Edge.Key;

    /// <summary>The words.</summary>
    public string Text => Edge.Label;

    /// <summary>The centre x on the world.</summary>
    public double X => Edge.MiddleX;

    /// <summary>The centre y on the world.</summary>
    public double Y => Edge.MiddleY;

    /// <summary>Whether the edge is suggested (the pill's border is dashed).</summary>
    public bool Dashed => Edge.Dashed;

    /// <summary>Whether the pill is always on (an edge between bands, or a conflict); in-band pills show on hover only.</summary>
    public bool Always { get; }
}
