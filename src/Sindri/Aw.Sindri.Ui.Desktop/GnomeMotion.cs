using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>
/// GNOME's reduce-motion setting, best effort: 'gsettings get org.gnome.desktop.interface enable-animations'. No
/// gsettings, a failure, a hang past 3 s or any answer but 'false' reads as no reduction.
/// </summary>
internal static class GnomeMotion
{
    /// <summary>The command that reads the setting.</summary>
    internal const string Command = "gsettings";

    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(3);

    /// <summary>Reads the setting.</summary>
    /// <param name="gsettings">The gsettings command.</param>
    /// <returns>True when animations are off.</returns>
    internal static bool Read(string gsettings)
    {
        var start = new ProcessStartInfo(gsettings, ["get", "org.gnome.desktop.interface", "enable-animations"])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        try
        {
            using var process = Process.Start(start);
            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEndAsync();
            if (!process.WaitForExit(_wait))
            {
                process.Kill();
                return false;
            }

#pragma warning disable RS0030 // the process exited and WaitForExit flushed the stream, so the read is finished and GetResult doesn't block
            return process.ExitCode == 0 && string.Equals(output.GetAwaiter().GetResult().Trim(), "false", StringComparison.Ordinal);
#pragma warning restore RS0030
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
