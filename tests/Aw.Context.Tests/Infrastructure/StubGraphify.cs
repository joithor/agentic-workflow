using Aw.Context.Infrastructure.Extract;
using Aw.Kernel.Testing;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>
/// A shell script standing in for graphify, with a pass-through sandbox, so the run path is exercised on a machine without bwrap.
/// '--version' prints the given version; any other call writes a marker file (its HOME, TMPDIR and the mode of HOME) and a graph.json with one community of every
/// .ts file under the folder it was asked to extract.
/// </summary>
internal sealed class StubGraphify : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("aw-stub-graphify-").FullName;

    public StubGraphify(string version, bool hangs = false)
    {
        RanMarker = Path.Combine(_folder, "ran");
        var script = Path.Combine(_folder, "graphify");
        _ = ExecutableScript.Write(script, string.Join('\n',
        [
            $"if [ \"$1\" = \"--version\" ]; then echo \"graphify {version}\"; exit 0; fi",
            .. hangs ? (string[])["sleep 300"] : [],   // a run that never ends: the caller's deadline has to cut it
            $"echo \"$HOME|$TMPDIR|$(stat -c %a \"$HOME\" 2>/dev/null || stat -f %Lp \"$HOME\")\" > \"{RanMarker}\"",
            "root=\"$2\"",
            "mkdir -p \"$root/graphify-out\"",
            "printf '{\"nodes\":[{\"id\":\"a\",\"community\":0,\"source_file\":\"%s/src/a.ts\"},{\"id\":\"b\",\"community\":0,\"source_file\":\"%s/src/b.ts\"}],\"links\":[]}' \"$root\" \"$root\" > \"$root/graphify-out/graph.json\"",
        ]) + "\n");

        Options = GraphifyOptions.Default with { Binary = script, Sandbox = static (_, command, _) => RunThroughShell(command) };
    }

    /// <summary>Runs the stub script through sh, as the pass-through sandbox of a machine without bwrap would.</summary>
    /// <param name="command">The command whose first element is the stub script.</param>
    public static IReadOnlyList<string> RunThroughShell(IReadOnlyList<string> command) => ["/bin/sh", .. command];

    public string RanMarker { get; }

    public GraphifyOptions Options { get; }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
