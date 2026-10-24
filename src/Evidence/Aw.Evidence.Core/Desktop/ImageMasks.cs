using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Images;

namespace Aw.Evidence.Core.Desktop;

/// <summary>Paints regions over an image, so what is allowed to differ never counts.</summary>
public static class ImageMasks
{
    private const uint MaskColour = 0xFF00FFFF;

    /// <summary>Returns a copy of <paramref name="image"/> with each region painted in one flat colour; a region is clipped to the image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="regions">The regions.</param>
    /// <returns>The masked copy (the same image when there is nothing to paint).</returns>
    public static RgbaImage Apply(RgbaImage image, IReadOnlyList<Region> regions)
    {
        if (regions.Count == 0)
        {
            return image;
        }

        var pixels = image.Pixels.ToArray();
        foreach (var region in regions)
        {
            var right = Math.Min(region.Right, image.Width);
            var bottom = Math.Min(region.Bottom, image.Height);
            for (var y = Math.Max(region.Y, 0); y < bottom; y++)
            {
                for (var x = Math.Max(region.X, 0); x < right; x++)
                {
                    var i = ((y * image.Width) + x) * 4;
                    (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]) = ((byte)(MaskColour >> 24), (byte)(MaskColour >> 16), (byte)(MaskColour >> 8), (byte)MaskColour);
                }
            }
        }

        return RgbaImage.Create(image.Width, image.Height, pixels).Value;
    }
}
