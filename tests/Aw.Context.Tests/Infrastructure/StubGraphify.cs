using Aw.Context.Infrastructure.Extract;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>
/// A shell script standing in for graphify, with a pass-through sandbox, so the run path is exercised on a machine without bwrap.
/// '--version' prints the given version; any other call writes a marker file (its HOME, TMPDIR and the mode of HOME) and a graph.json with one community of every
/// .ts file under the folder it was asked to extract.
/// </summary>
internal sealed class StubGraphify : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("aw-stub-graphify-").FullName;

    public StubGraphify(string version)
    {
        RanMarker = Path.Combine(_folder, "ran");
        var script = Path.Combine(_folder, "graphify");
        File.WriteAllLines(script,
        [
            "#!/bin/sh",
            $"if [ \"$1\" = \"--version\" ]; then echo \"graphify {version}\"; exit 0; fi",
            $"echo \"$HOME|$TMPDIR|$(stat -c %a \"$HOME\" 2>/dev/null || stat -f %Lp \"$HOME\")\" > \"{RanMarker}\"",
            "root=\"$2\"",
            "mkdir -p \"$root/graphify-out\"",
            "printf '{\"nodes\":[{\"id\":\"a\",\"community\":0,\"source_file\":\"%s/src/a.ts\"},{\"id\":\"b\",\"community\":0,\"source_file\":\"%s/src/b.ts\"}],\"links\":[]}' \"$root\" \"$root\" > \"$root/graphify-out/graph.json\"",
        ]);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Options = GraphifyOptions.Default with { Binary = script, Sandbox = static (_, command, _) => RunThroughShell(command) };
    }

    /// <summary>Reads the script with sh instead of exec'ing it: Linux refuses to exec a file a concurrent fork still holds open for writing (ETXTBSY), which made runs flake.</summary>
    /// <param name="command">The command whose first element is the stub script.</param>
    public static IReadOnlyList<string> RunThroughShell(IReadOnlyList<string> command) => ["/bin/sh", .. command];

    public string RanMarker { get; }

    public GraphifyOptions Options { get; }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
