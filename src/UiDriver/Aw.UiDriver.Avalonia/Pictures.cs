using Avalonia;
using Avalonia.Media.Imaging;
using Aw.Kernel;
using Aw.UiDriver.Core;

namespace Aw.UiDriver.Avalonia;

/// <summary>Renders an element to PNG bytes: the element as it is drawn now, at scale 1.</summary>
internal static class Pictures
{
    /// <summary>The element's picture.</summary>
    /// <param name="visual">The element.</param>
    /// <returns>The PNG bytes, or AW-UIDRIVER-007 when it has no size or could not be rendered.</returns>
    internal static Result<byte[]> Png(Visual visual)
    {
        var size = new PixelSize((int)Math.Round(visual.Bounds.Width), (int)Math.Round(visual.Bounds.Height));
        if (size.Width < 1 || size.Height < 1)
        {
            return UiDriverErrors.Failed.With("the element has no size to draw");
        }

        try
        {
            using var frame = new RenderTargetBitmap(size, new Vector(96, 96));
            frame.Render(visual);
            using var stream = new MemoryStream();
            frame.Save(stream);
            return stream.ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return UiDriverErrors.Failed.With($"the picture could not be made: {e.Message}");
        }
    }
}
