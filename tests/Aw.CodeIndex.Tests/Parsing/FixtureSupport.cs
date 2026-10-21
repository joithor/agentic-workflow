using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Aw.CodeIndex.Tests.Parsing;

/// <summary>Where the code-index fixtures are, and the two outside tools the fixture tests use: node (the hostile-file generator) and git (a commit's files).</summary>
internal static class FixtureSupport
{
    public static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No AgenticWorkflow.slnx above the test's base directory.");
    }

    public static string FixtureRoot() => Path.Combine(RepoRoot(), "tests", "fixtures", "code-index");

    public static JsonDocument ReadJson(string relative) => JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot(), relative)));

    /// <summary>Runs 'node generate.mjs --out &lt;directory&gt;' for the hostile files and returns its manifest rows.</summary>
    public static IReadOnlyList<(string Path, long Size, string Outcome)> GenerateHostile(string directory)
    {
        var start = new ProcessStartInfo("node") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(FixtureRoot(), "hostile", "generate.mjs"));
        start.ArgumentList.Add("--out");
        start.ArgumentList.Add(directory);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("node did not start");
        var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, string.Create(CultureInfo.InvariantCulture, $"generate.mjs exited {process.ExitCode}: {error.Result}"));

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")));
        var rows = manifest.RootElement.ValueKind == JsonValueKind.Array ? manifest.RootElement : manifest.RootElement.GetProperty("files");
        return [.. rows.EnumerateArray().Select(r => (r.GetProperty("path").GetString()!, r.GetProperty("size").GetInt64(), r.GetProperty("outcome").GetString()!))];
    }

    /// <summary>The '.cs' files under a path of one commit, read through one 'git cat-file --batch' process.</summary>
    public static IReadOnlyDictionary<string, string> CommitSources(string commit, string underPath)
    {
        var root = RepoRoot();
        var paths = CommitPaths(commit, underPath, ".cs");
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, WorkingDirectory = root };
        start.ArgumentList.Add("cat-file");
        start.ArgumentList.Add("--batch");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git did not start");
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        var output = process.StandardOutput.BaseStream;
        foreach (var path in paths)
        {
            process.StandardInput.Write($"{commit}:{path}\n");
            process.StandardInput.Flush();
            var header = ReadLine(output).Split(' ');
            var size = int.Parse(header[2], CultureInfo.InvariantCulture);
            var bytes = new byte[size];
            output.ReadExactly(bytes);
            _ = output.ReadByte();   // the LF after the content
            sources[path] = Encoding.UTF8.GetString(bytes);
        }

        process.StandardInput.Close();
        process.WaitForExit();
        return sources;
    }

    public static IReadOnlyList<string> CommitPaths(string commit, string underPath, string extension) =>
        [.. Git(RepoRoot(), "ls-tree", "-r", "--name-only", commit, "--", underPath).Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(p => p.EndsWith(extension, StringComparison.Ordinal))];

    public static bool CommitExists(string commit)
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = RepoRoot() };
        foreach (var argument in new[] { "cat-file", "-e", commit + "^{commit}" })
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static string Git(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, WorkingDirectory = directory };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("git did not start");
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    private static string ReadLine(Stream stream)
    {
        var line = new StringBuilder();
        int next;
        while ((next = stream.ReadByte()) is not (-1 or '\n'))
        {
            line.Append((char)next);
        }

        return line.ToString();
    }
}
