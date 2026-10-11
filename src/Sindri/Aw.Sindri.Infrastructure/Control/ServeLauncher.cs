using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// Starts '&lt;this binary&gt; serve' through /bin/sh with exec, so the pid is the control plane's, stdin is /dev/null and
/// its output is appended to &lt;home&gt;/logs/serve.log: the child keeps no pipe of the terminal that started it.
/// </summary>
internal sealed class ServeLauncher(SindriSettings settings) : IControlPlaneLauncher
{
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

        var process = Process.Start(new ProcessStartInfo("/bin/sh", ["-c", "exec \"$0\" serve </dev/null >>\"$1\" 2>&1", self, log])
        {
            UseShellExecute = false,
        });
        return process is null
            ? SindriErrors.StartFailed.With("/bin/sh didn't start")
            : new Launched(process, log);
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
