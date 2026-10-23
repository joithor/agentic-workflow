namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A band's label and its vertical extent, shared by every column.</summary>
/// <param name="Band">The band.</param>
/// <param name="Top">Where it starts.</param>
/// <param name="Height">How tall it is.</param>
internal sealed record PlacedBand(WholeBand Band, double Top, double Height);
