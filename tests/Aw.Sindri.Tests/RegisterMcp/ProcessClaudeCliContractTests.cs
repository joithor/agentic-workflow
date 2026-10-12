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

    protected override IClaudeCli Create(int? exitCode, string error)
    {
        var stub = Path.Combine(_root.FullName, "claude");
        if (exitCode is { } code)
        {
            var errorFile = Path.Combine(_root.FullName, "stderr.txt");
            File.WriteAllText(errorFile, error);
            File.WriteAllText(stub, $"#!/bin/sh\nfor a in \"$@\"; do printf '%s\\n' \"$a\"; done >> '{Log}'\nprintf '%s\\n%s\\n' '{Marker}' \"$(pwd -P)\" >> '{Log}'\ncat '{errorFile}' >&2\nexit " + code.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(stub, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
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

    private string Log => Path.Combine(_root.FullName, "runs.log");
}
