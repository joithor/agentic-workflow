using Aw.Kernel.Testing;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Mcp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RegisterMcp;

/// <summary>The process adapter over a recording stub script (never the real claude): it logs each argument on its own line, then the directory.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessClaudeCliContractTests : ClaudeCliContract, IDisposable
{
    private const string Marker = "--cwd--";
    private readonly DirectoryInfo _root = System.IO.Directory.CreateTempSubdirectory("claude-stub-");

    public void Dispose() => _root.Delete(recursive: true);

    [Fact]
    public async Task A_bare_name_runs_from_the_search_path_never_from_the_working_directory()
    {
        var onPath = System.IO.Directory.CreateDirectory(Path.Combine(_root.FullName, "on-path")).FullName;
        var work = System.IO.Directory.CreateDirectory(Path.Combine(_root.FullName, "work")).FullName;
        _ = ExecutableScript.Write(Path.Combine(onPath, "claude"), $"echo path >> '{Log}'\n");
        _ = ExecutableScript.Write(Path.Combine(work, "claude"), $"echo cwd >> '{Log}'\n");
        await File.WriteAllTextAsync(Path.Combine(onPath, "not-exec"), "x", TestContext.Current.CancellationToken);
        var settings = new McpSettings("claude", _root.FullName, ClaudeConfigDir: null, CodexHome: null) { SearchPath = ["relative", onPath] };

        var run = await new ProcessClaudeCli(settings).Run(["mcp"], work, TestContext.Current.CancellationToken);
        var nonExecutable = await new ProcessClaudeCli(settings with { ClaudeBin = "not-exec" }).Run(["mcp"], work, TestContext.Current.CancellationToken);

        Assert.Equal(0, run!.ExitCode);   // why: the stub on the search path started
        Assert.Equal(["path"], await File.ReadAllLinesAsync(Log, TestContext.Current.CancellationToken));
        Assert.Null(nonExecutable);
    }

    [Fact]
    public void The_search_path_is_PATH_s_absolute_entries_in_order()
    {
        var settings = McpSettings.Read(name => name == "PATH" ? "/a::relative:/b:./c" : null);

        Assert.Equal(["/a", "/b"], settings.SearchPath);
    }

    [Fact]
    public async Task A_run_over_its_budget_is_killed_and_reads_124()
    {
        var stub = Path.Combine(_root.FullName, "slow-claude");
        _ = ExecutableScript.Write(stub, "sleep 30\n");

        var cli = new ProcessClaudeCli(new McpSettings(stub, _root.FullName, ClaudeConfigDir: null, CodexHome: null) { ClaudeBudget = TimeSpan.FromSeconds(1) });
        var started = System.Diagnostics.Stopwatch.StartNew();

        var run = await cli.Run(["mcp"], Directory(), TestContext.Current.CancellationToken);

        Assert.Equal(new ClaudeRun(124, "claude didn't finish within 1 s"), run);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(10), started.Elapsed.ToString());
    }

    protected override IClaudeCli Create(int? exitCode, string error)
    {
        var stub = Path.Combine(_root.FullName, "claude");
        if (exitCode is { } code)
        {
            var errorFile = Path.Combine(_root.FullName, "stderr.txt");
            File.WriteAllText(errorFile, error);
            _ = ExecutableScript.Write(stub, $"for a in \"$@\"; do printf '%s\\n' \"$a\"; done >> '{Log}'\nprintf '%s\\n%s\\n' '{Marker}' \"$(pwd -P)\" >> '{Log}'\ncat '{errorFile}' >&2\nexit " + code.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");
        }

        return new ProcessClaudeCli(new McpSettings(stub, _root.FullName, ClaudeConfigDir: null, CodexHome: null));
    }

    protected override IReadOnlyList<(IReadOnlyList<string> Arguments, string Directory)> Received()
    {
        var runs = new List<(IReadOnlyList<string>, string)>();
        var arguments = new List<string>();
        var lines = File.Exists(Log) ? File.ReadAllLines(Log) : [];
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i] == Marker)
            {
                runs.Add(([.. arguments], lines[++i]));
                arguments.Clear();
            }
            else
            {
                arguments.Add(lines[i]);
            }
        }

        return runs;
    }

    protected override string Directory() => RealPath.Of(_root.FullName)!;   // why: the temp root exists; the stub prints the physical path (pwd -P)

    protected override string MissingDirectory() => Path.Combine(_root.FullName, "gone");

    private string Log => Path.Combine(_root.FullName, "runs.log");
}
