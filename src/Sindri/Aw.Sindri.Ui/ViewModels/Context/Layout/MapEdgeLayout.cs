
namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>One edge placed on the world, with the words for its label pill.</summary>
/// <param name="Key">The link id when served, else "from>to:type"; unique among the edges.</param>
/// <param name="From">The node key at one end.</param>
/// <param name="To">The node key at the other end.</param>
/// <param name="Type">The served edge type ("decides", "part-of", "conflicts"…).</param>
/// <param name="Route">The path drawn.</param>
/// <param name="Dashed">Whether it is suggested (not confirmed, or found by the model).</param>
/// <param name="CrossesBands">Whether it joins two bands (its pill is always on).</param>
/// <param name="Label">The pill's words.</param>
/// <param name="Middle">Where the pill's centre sits.</param>
/// <param name="Link">The link id when served, else null.</param>
internal sealed record MapEdgeLayout(string Key, string From, string To, string Type, EdgeRoute Route, bool Dashed, bool CrossesBands, string Label, MapPoint Middle, string? Link)
{
    /// <summary>Gets the pill's centre x, px.</summary>
    internal double MiddleX => Middle.X;

    /// <summary>Gets the pill's centre y, px.</summary>
    internal double MiddleY => Middle.Y;
}
