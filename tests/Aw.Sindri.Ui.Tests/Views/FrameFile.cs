using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// Saves a key frame as frames/&lt;name&gt;.png under the test output. The project draws headless by default, which renders no
/// pixels, so a run that expects PNGs (a stage's owner check, an evidence run) gets none unless AW_UI_FRAMES=1; the first frame
/// skipped says so on stderr instead of the PNGs going missing without a word.
/// </summary>
public static class FrameFile
{
    private static int _warned;

    /// <summary>Captures the window and writes frames/<paramref name="name"/>.png; with headless drawing it writes nothing and says why, once per run.</summary>
    public static void Save(Window window, string name)
    {
        var frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            if (Interlocked.Exchange(ref _warned, 1) == 0)
            {
                Console.Error.WriteLine($"aw-ui-frames: no PNG written for '{name}' (and none will be): headless drawing renders no pixels; set AW_UI_FRAMES=1 to save the key frames.");
            }

            return;
        }

        using (frame)
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "frames");
            _ = Directory.CreateDirectory(dir);
            frame.Save(Path.Combine(dir, name + ".png"), new PngBitmapEncoderOptions());
        }
    }
}
