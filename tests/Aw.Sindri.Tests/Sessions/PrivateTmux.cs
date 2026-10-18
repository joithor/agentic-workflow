using System.Diagnostics;
using System.Globalization;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>
/// A tmux server of the test's own on the private socket name 'aw-co2b-&lt;pid&gt;' (tmux -L), with one session. It is started and killed here only: never the
/// default server, never a team's socket. Every child runs without TMUX and TMUX_PANE.
/// </summary>
internal sealed class PrivateTmux : IDisposable
{
    private readonly string _name = Name();

    private PrivateTmux(string socket)
    {
        Socket = socket;
    }

    /// <summary>The absolute path of the private server's socket.</summary>
    public string Socket { get; }

    /// <summary>Starts the private server.</summary>
    /// <returns>The server; null when tmux isn't installed or the server didn't start.</returns>
    public static PrivateTmux? Start()
    {
        var name = Name();
        if (Run(name, "new-session", "-d", "-s", "co2b", "sleep 300") is null)
        {
            return null;
        }

        var socket = Run(name, "display-message", "-p", "#{socket_path}")?.Trim();
        if (socket is { Length: > 0 } && Path.IsPathRooted(socket))
        {
            return new PrivateTmux(socket);
        }

        _ = Run(name, "kill-server");
        return null;
    }

    private static string Name() => string.Create(CultureInfo.InvariantCulture, $"aw-co2b-{Environment.ProcessId}");

    public void Dispose() => _ = Run(_name, "kill-server");   // this -L socket only

    private static string? Run(string name, params string[] arguments)
    {
        var start = new ProcessStartInfo("tmux") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var part in (string[])["-L", name, .. arguments])
        {
            start.ArgumentList.Add(part);
        }

        _ = start.Environment.Remove("TMUX");
        _ = start.Environment.Remove("TMUX_PANE");
        try
        {
            using var child = Process.Start(start);
            if (child is null)
            {
                return null;
            }

            var output = child.StandardOutput.ReadToEnd();
            _ = child.StandardError.ReadToEnd();
            child.WaitForExit();
            return child.ExitCode == 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
