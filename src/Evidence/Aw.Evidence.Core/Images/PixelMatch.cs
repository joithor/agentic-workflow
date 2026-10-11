using System.Globalization;
using Aw.Evidence.Core.Geometry;
using Aw.Kernel;

namespace Aw.Evidence.Core.Images;

/// <summary>
/// Compares two same-size RGBA images pixel by pixel with a perceptual (YIQ) colour delta and anti-aliasing detection.
/// Deterministic and model-free: the gate in front of any visual critique.
/// </summary>
public static class PixelMatch
{
    private const double FadeAlpha = 0.1;

    /// <summary>Compares <paramref name="expected"/> with <paramref name="actual"/>.</summary>
    /// <param name="expected">The reference image (design or baseline).</param>
    /// <param name="actual">The image under test.</param>
    /// <param name="options">Threshold and anti-aliasing handling.</param>
    /// <returns>The comparison, or AW-EVIDENCE-008 when the sizes differ.</returns>
    public static Result<PixelComparison> Compare(RgbaImage expected, RgbaImage actual, PixelMatchOptions options)
    {
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return EvidenceErrors.SizeMismatch.With(
                string.Create(CultureInfo.InvariantCulture, $"size mismatch: {expected.Width}x{expected.Height} vs {actual.Width}x{actual.Height}"));
        }

        var width = expected.Width;
        var height = expected.Height;
        var diff = new byte[width * height * 4];
        var tracker = new ChangeTracker(width, height);
        var pass = new DiffPass(new ImagePair(expected.Pixels, actual.Pixels, width, height), diff, options, tracker);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pass.ComparePixel(x, y);
            }
        }

        return new PixelComparison(tracker.Count, (long)width * height, RgbaImage.Own(width, height, diff), tracker.Box());
    }

    private readonly ref struct DiffPass(ImagePair pair, Span<byte> diff, PixelMatchOptions options, ChangeTracker tracker)
    {
        private readonly ImagePair _pair = pair;
        private readonly Span<byte> _diff = diff;
        private readonly double _maxDelta = YiqColor.MaxDelta * options.Threshold * options.Threshold;

        public void ComparePixel(int x, int y)
        {
            var pos = _pair.Offset(x, y);
            var delta = YiqColor.Delta(_pair.Image, _pair.Other, pos, pos, brightnessOnly: false);
            if (Math.Abs(delta) <= _maxDelta)
            {
                var grey = YiqColor.FadedGrey(_pair.Image, pos, FadeAlpha);
                Paint(pos, grey, grey, grey);
            }
            else if (!options.IncludeAA && (AntiAliasing.IsAntiAliased(_pair, x, y) || AntiAliasing.IsAntiAliased(_pair.Swapped, x, y)))
            {
                Paint(pos, 255, 255, 0);
            }
            else
            {
                Paint(pos, 255, 0, 0);
                tracker.Add(x, y);
            }
        }

        private void Paint(int pos, byte r, byte g, byte b)
        {
            _diff[pos] = r;
            _diff[pos + 1] = g;
            _diff[pos + 2] = b;
            _diff[pos + 3] = 255;
        }
    }

    private sealed class ChangeTracker(int width, int height)
    {
        private int _minX = width, _minY = height, _maxX = -1, _maxY = -1;

        public long Count { get; private set; }

        public void Add(int x, int y)
        {
            Count++;
            _minX = Math.Min(_minX, x);
            _minY = Math.Min(_minY, y);
            _maxX = Math.Max(_maxX, x);
            _maxY = Math.Max(_maxY, y);
        }

        public Region? Box() => Count == 0 ? null : new Region(_minX, _minY, _maxX - _minX + 1, _maxY - _minY + 1);
    }
}
