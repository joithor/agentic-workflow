using Avalonia;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop head's entry point: claim the single instance, then run the app until Quit (or hand over and exit).</summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var platform = Composition.Platform(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), TimeProvider.System);
        try
        {
            return SingleLaunch.Run(platform.Instance, args, () => Composition.Compose(platform, TimeProvider.System).StartWithClassicDesktopLifetime(args), Console.Error);
        }
        finally
        {
            platform.Instance.DisposeAsync().AsTask().GetAwaiter().GetResult();
            (platform.Motion as IDisposable)?.Dispose();
        }
    }
}
