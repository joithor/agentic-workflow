using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// Starts '&lt;this binary&gt; serve' through /bin/sh with exec, so the pid is the control plane's, stdin is /dev/null and
/// its output is appended to &lt;home&gt;/logs/serve.log (0600, under umask 077): the child keeps no pipe of the terminal
/// that started it. It runs in its own session (perl's POSIX::setsid before the exec), so a Ctrl-C during the start wait
/// or a kill of the starting terminal's process group doesn't reach it; without /usr/bin/perl it is a plain exec.
/// </summary>
internal sealed class ServeLauncher(SindriSettings settings) : IControlPlaneLauncher
{
    private const string Perl = "/usr/bin/perl";

    public Result<ILaunchedProcess> Launch()
    {
        var logs = Path.Combine(settings.Home, "logs");
        OwnerOnly.EnsureDirectory(settings.Home);
        OwnerOnly.EnsureDirectory(logs);
        var log = Path.Combine(logs, "serve.log");
        var self = Environment.ProcessPath;
        if (self is null)
        {
            return SindriErrors.StartFailed.With("this process's executable path is unknown, so 'sindri serve' can't be started");
        }

        var detach = File.Exists(Perl) ? $"exec {Perl} -MPOSIX -e 'POSIX::setsid(); exec @ARGV'" : "exec";
        var process = Process.Start(new ProcessStartInfo("/bin/sh", ["-c", $"umask 077; {detach} \"$0\" serve </dev/null >>\"$1\" 2>&1", self, log])
        {
            UseShellExecute = false,
        });
        if (process is null)
        {
            return SindriErrors.StartFailed.With("/bin/sh didn't start");
        }

        OwnerOnly.RestrictFiles(log);   // a log left from before, with wider permissions
        return new Launched(process, log);
    }

    private sealed class Launched(Process process, string log) : ILaunchedProcess
    {
        public int Pid => process.Id;

        public string LogPath => log;

        public bool HasExited => process.HasExited;

        public string LastError() => File.Exists(log)
            ? File.ReadLines(log).LastOrDefault(l => l.StartsWith("SND-", StringComparison.Ordinal)) ?? "no error line in the log"
            : "no log";

        public void Dispose() => process.Dispose();
    }
}
