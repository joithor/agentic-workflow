namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>One band's frame on the world and its two lines of words.</summary>
/// <param name="Band">Which band.</param>
/// <param name="Top">The band's top edge.</param>
/// <param name="Height">The band's height.</param>
/// <param name="Title">The band's name.</param>
/// <param name="Meta">The counts on the band's right.</param>
internal sealed record MapBandLayout(MapBand Band, double Top, double Height, string Title, string Meta);
