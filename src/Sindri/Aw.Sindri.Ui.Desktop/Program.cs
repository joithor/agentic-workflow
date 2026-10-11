using Avalonia;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop head's entry point: compose the app, then run it until its window closes.</summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args) =>
        Composition.Compose(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), TimeProvider.System)
            .StartWithClassicDesktopLifetime(args);
}
