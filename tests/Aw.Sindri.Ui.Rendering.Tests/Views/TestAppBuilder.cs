using Avalonia;
using Avalonia.Headless;
using Aw.Sindri.Ui.Rendering.Tests.Views;
using Aw.Sindri.Ui.Views;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>The app's theme under Avalonia's headless platform, drawn by Skia so a test can read the pixels of a frame.</summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<SindriApp>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
