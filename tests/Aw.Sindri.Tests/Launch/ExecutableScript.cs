using System.ComponentModel;
using System.Diagnostics;
using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>Writes a shell script a test is about to run as a program. A fresh executable can fail to start with ETXTBSY ('text file busy') while a parallel test forks: the child inherits our write descriptor until it execs. Renaming does not help (same inode), so the script is run once, here, until it starts; after that no write descriptor is left and the code under test sees a normal executable.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal static class ExecutableScript
{
    private const int TextFileBusy = 26;   // ETXTBSY on Linux and macOS
    private const string WarmupVariable = "AW_TEST_SCRIPT_WARMUP";

    public static string Write(string path, string body)
    {
        var script = "#!/bin/sh\n[ -n \"$" + WarmupVariable + "\" ] && exit 0\n" + body;
        File.WriteAllText(path, script);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        WarmUp(path);
        return path;
    }

    private static void WarmUp(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            var info = new ProcessStartInfo(path) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            info.Environment[WarmupVariable] = "1";
            try
            {
                using var process = Process.Start(info)!;
                process.WaitForExit();
                return;
            }
            catch (Win32Exception e) when (e.NativeErrorCode == TextFileBusy && attempt < 100)
            {
                RealTime.Sleep(TimeSpan.FromMilliseconds(20));   // only the exec-busy case is retried; any other start failure surfaces
            }
        }
    }
}
