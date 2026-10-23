namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The box the camera eases to for a query's answer (whole-map spec §3.1 "Zoomed in").</summary>
internal static class WholeZoomBox
{
    /// <summary>The space kept round the answer's nodes.</summary>
    internal const double Padding = 48;

    /// <summary>The smallest box, so a single node still gets some of the map round it.</summary>
    internal const double MinSize = 360;

    /// <summary>The box round some rectangles: their union, padded, at least <see cref="MinSize"/> in each direction and centred on the union.</summary>
    /// <param name="rects">The answer's nodes.</param>
    /// <returns>The box.</returns>
    internal static WholeRect Around(IReadOnlyList<WholeRect> rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        if (rects.Count == 0)
        {
            return default;
        }

        var (left, top, right, bottom) = (rects.Min(r => r.X), rects.Min(r => r.Y), rects.Max(r => r.Right), rects.Max(r => r.Bottom));
        var (width, height) = (Math.Max(MinSize, right - left + (2 * Padding)), Math.Max(MinSize, bottom - top + (2 * Padding)));
        return new WholeRect(((left + right) / 2) - (width / 2), ((top + bottom) / 2) - (height / 2), width, height);
    }
}
