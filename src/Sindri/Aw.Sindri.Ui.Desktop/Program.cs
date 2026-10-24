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
        var log = telemetry.LoggerFactory.CreateLogger("Aw.Sindri.Ui");
        // SingleLaunch composes the platform, runs or hands over, then ends the instance and the motion poll, all inside
        // its SND-UI-004 boundary.
        // Capture mode (desktop UI evidence): the real window at a fixed size, each screen saved as a PNG, then the app ends.
        var capture = CaptureArguments.Asked(args) ? CaptureArguments.Parse(args, static problem => Console.Error.WriteLine("sindri-ui: capture: " + problem)) : null;
        if (CaptureArguments.Asked(args) && capture is null)
        {
            Console.Error.WriteLine("sindri-ui: usage: " + CaptureArguments.Usage);
            return 1;
        }

        return SingleLaunch.Run(
            () => Composition.Platform(Environment.GetEnvironmentVariable, home, TimeProvider.System, capture is not null) with { Log = log },
            capture is null ? args : Array.Empty<string>(),
            platform => Composition.Compose(platform, TimeProvider.System, capture).StartWithClassicDesktopLifetime(args),
            Console.Error,
            log);
    }
}
