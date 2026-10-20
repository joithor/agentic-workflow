using System.Diagnostics;

namespace Aw.Kernel.Testing.Tests;

[Lane(TestLane.Integration)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ExecutableScriptTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    [Fact]
    public void A_written_script_runs_and_the_warm_up_left_no_trace_in_its_output()
    {
        var log = Path.Combine(_dir.Path, "calls.log");
        var path = ExecutableScript.Write(Path.Combine(_dir.Path, "tool"), $"echo ran >> '{log}'\necho \"out:$1\"\n");

        Assert.False(File.Exists(log), "the warm-up run must exit before the body");
        var output = Run(path, "x");

        Assert.Equal("out:x\n", output);
        Assert.Equal("ran\n", File.ReadAllText(log));
    }

    [Fact]
    public void The_interpreter_names_the_shebang()
    {
        var path = ExecutableScript.Write(Path.Combine(_dir.Path, "tool"), "echo hi\n", "/usr/bin/env sh");

        Assert.StartsWith("#!/usr/bin/env sh\n", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Equal("hi\n", Run(path));
    }

    private static string Run(string path, params string[] arguments)
    {
        var info = new ProcessStartInfo(path) { UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
