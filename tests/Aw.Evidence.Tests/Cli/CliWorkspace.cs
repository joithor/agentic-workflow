using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aw.Evidence.Tests.Cli;

/// <summary>A temp directory with its own state root and lock paths, and the built ui-evidence to run against them.</summary>
internal sealed class CliWorkspace : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aw-evidence-cli-");

    public string Root => _root.FullName;

    public string QaStackLock => Path.Combine(Root, "locks", "qa-stack.lock");

    public string HeavyJobLock => Path.Combine(Root, "locks", "heavy-job.lock");

    public string Write(string relative, string content)
    {
        var path = Path.Combine(Root, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: a path under the root always has a directory
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Runs ui-evidence in the workspace with its own lock paths and no inherited heavy-job lock.</summary>
    public (int Exit, string Out, string Err) Run(params string[] args)
    {
        var start = new ProcessStartInfo(DotnetHost())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Root,
        };
        foreach (var arg in (string[])["exec", Path.Combine(AppContext.BaseDirectory, "ui-evidence.dll"), .. args])
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["AW_STATE_DIR"] = Path.Combine(Root, "state");
        start.Environment["AW_QA_STACK_LOCK"] = QaStackLock;
        start.Environment["AW_HEAVY_JOB_LOCK"] = HeavyJobLock;
        _ = start.Environment.Remove("AW_HEAVY_LOCK_HELD");
        start.Environment["AW_LOG_LEVEL"] = "none";   // the tests compare stderr
        using var process = Process.Start(start)!;   // why: Process.Start returns null only for a shell-executed document
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output.Result, error);
    }

    public void Dispose() => _root.Delete(recursive: true);

    private static string DotnetHost()
    {
        // The runtime directory is <dotnet root>/shared/Microsoft.NETCore.App/<version>/.
        var root = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
        return Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
    }
}
