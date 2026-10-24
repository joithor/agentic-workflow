namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A band's vertical extent, shared by every column.</summary>
/// <param name="Band">The band.</param>
/// <param name="Top">Where it starts.</param>
/// <param name="Height">How tall it is.</param>
/// <param name="Meta">What is in it, in words ("3 records · 1 decision"); empty when nothing is.</param>
internal sealed record PlacedBand(WholeBand Band, double Top, double Height, string Meta = "")
{
    /// <summary>Gets where the band's lane starts: its title sits in the strip above the first row.</summary>
    internal double LaneTop => Top - WholeMapLayout.LaneLabel;

    /// <summary>Gets where the band's lane ends.</summary>
    internal double LaneBottom => Top + Height + WholeMapLayout.LanePad;
}
