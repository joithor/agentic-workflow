using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One band's frame as the view draws it.</summary>
/// <param name="Top">Top edge on the world.</param>
/// <param name="Height">Height.</param>
/// <param name="Width">Width (the world's).</param>
/// <param name="Title">The band's name.</param>
/// <param name="Meta">The counts on the right.</param>
internal sealed record MapBandViewModel(double Top, double Height, double Width, string Title, string Meta)
{
    /// <summary>The band's name with its counts, for assistive technology.</summary>
    public string Name => Title + ". " + Meta;

    /// <summary>A band from its layout.</summary>
    /// <param name="band">The layout.</param>
    /// <param name="width">The world's width.</param>
    /// <returns>The view model.</returns>
    internal static MapBandViewModel From(MapBandLayout band, double width) => new(band.Top, band.Height, width, band.Title, band.Meta);
}
