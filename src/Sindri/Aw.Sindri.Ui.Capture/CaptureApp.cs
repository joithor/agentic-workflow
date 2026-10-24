using Avalonia;
using Avalonia.Headless;
using Aw.Sindri.Ui.Views;

namespace Aw.Sindri.Ui.Capture;

/// <summary>The headless app the capture runs in: the app's theme, Skia drawing (a frame has real pixels) and the Inter font.</summary>
public static class CaptureApp
{
    /// <summary>Builds the app for Avalonia's headless session (found by name, like a test application's builder).</summary>
    /// <returns>The builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<SindriApp>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
