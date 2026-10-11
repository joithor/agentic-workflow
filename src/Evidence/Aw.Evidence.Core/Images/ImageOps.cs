using System.Globalization;
using Aw.Evidence.Core.Geometry;
using Aw.Kernel;

namespace Aw.Evidence.Core.Images;

/// <summary>Crops and compositions over in-memory images.</summary>
public static class ImageOps
{
    /// <summary>The gap between panels of a side-by-side, in pixels.</summary>
    public const int SideBySideGap = 12;

    /// <summary>Copies <paramref name="region"/> out of <paramref name="image"/>. A region not wholly inside is an error, never a silent clip.</summary>
    /// <param name="image">The source.</param>
    /// <param name="region">The region.</param>
    /// <returns>The crop, or AW-EVIDENCE-007.</returns>
    public static Result<RgbaImage> Crop(RgbaImage image, Region region)
    {
        if (!region.FitsIn(image.Width, image.Height))
        {
            return EvidenceErrors.RegionOutside.With(string.Create(CultureInfo.InvariantCulture, $"region {region.Describe()} is outside the {image.Width}x{image.Height} image"));
        }

        var output = new byte[region.W * region.H * 4];
        Blit(image, output, region.W, region, 0);
        return RgbaImage.Own(region.W, region.H, output);
    }

    /// <summary>Crops <paramref name="box"/> grown by <paramref name="pad"/> on every side, clamped to the image.</summary>
    /// <param name="image">The source.</param>
    /// <param name="box">The region of interest.</param>
    /// <param name="pad">Padding in pixels (default 24).</param>
    /// <returns>The padded crop, or AW-EVIDENCE-007 when the box doesn't touch the image.</returns>
    public static Result<RgbaImage> CropPadded(RgbaImage image, Region box, int pad = 24)
    {
        var x = Math.Max(0, box.X - pad);
        var y = Math.Max(0, box.Y - pad);
        var w = Math.Min(image.Width - x, box.W + (2 * pad));
        var h = Math.Min(image.Height - y, box.H + (2 * pad));
        return Crop(image, new Region(x, y, w, h));
    }

    /// <summary>Design | implementation | diff on white, separated by <see cref="SideBySideGap"/> pixels, top-aligned.</summary>
    /// <param name="design">The left panel.</param>
    /// <param name="implementation">The middle panel.</param>
    /// <param name="diff">The right panel.</param>
    /// <returns>The composite.</returns>
    public static RgbaImage SideBySide(RgbaImage design, RgbaImage implementation, RgbaImage diff)
    {
        var width = design.Width + implementation.Width + diff.Width + (SideBySideGap * 2);
        var height = Math.Max(design.Height, Math.Max(implementation.Height, diff.Height));
        var output = new byte[width * height * 4];
        output.AsSpan().Fill(255);
        Blit(design, output, width, design.Bounds(), 0);
        Blit(implementation, output, width, implementation.Bounds(), design.Width + SideBySideGap);
        Blit(diff, output, width, diff.Bounds(), design.Width + implementation.Width + (SideBySideGap * 2));
        return RgbaImage.Own(width, height, output);
    }

    private static void Blit(RgbaImage source, Span<byte> target, int targetWidth, Region from, int toX)
    {
        for (var row = 0; row < from.H; row++)
        {
            var src = source.Pixels.Slice((((from.Y + row) * source.Width) + from.X) * 4, from.W * 4);
            src.CopyTo(target[(((row * targetWidth) + toX) * 4)..]);
        }
    }
}
