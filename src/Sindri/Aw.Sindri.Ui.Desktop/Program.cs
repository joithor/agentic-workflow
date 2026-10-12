using Avalonia;
using Aw.Kernel.Telemetry;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop head's entry point: claim the single instance, then run the app until Quit (or hand over and exit).</summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // JSON log lines on standard error (AW_LOG_LEVEL); the app logs a failed start-up here (SND-UI-004).
        using var telemetry = TelemetryRuntime.Start(TelemetryOptions.FromEnvironment("sindri-ui", Environment.GetEnvironmentVariable, TimeProvider.System));
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var platform = Composition.Platform(Environment.GetEnvironmentVariable, home, TimeProvider.System) with
        {
            Log = telemetry.LoggerFactory.CreateLogger("Aw.Sindri.Ui"),
        };
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
