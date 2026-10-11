using System.Diagnostics;

namespace Aw.BugFix.Tests.Contracts;

/// <summary>A temporary directory (its real path) removed on dispose, with a git helper for building repositories.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        var created = Directory.CreateTempSubdirectory("aw-bugfix-").FullName;
        Path = RealPath(created);
    }

    public string Path { get; }

    public static string Git(string directory, params string[] args)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in (string[])["-C", directory, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        using var process = Process.Start(start)!; // why: Start returns null only when UseShellExecute reuses a process; it is off.
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }

    public string Repo(string name, IReadOnlyDictionary<string, string> files)
    {
        var root = System.IO.Path.Combine(Path, name);
        foreach (var (file, text) in files)
        {
            Write(root, file, text);
        }

        _ = Git(root, "init", "-q", "-b", "main");
        _ = Git(root, "add", "-A");
        _ = Git(root, "commit", "-q", "-m", "base");
        return root;
    }

    public static void Write(string root, string file, string text)
    {
        var path = System.IO.Path.Combine(root, file);
        _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!); // why: a file path inside root always has a directory.
        File.WriteAllText(path, text);
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);

    // macOS hands out /var/folders/…, which is a link to /private/var/…; git reports the resolved spelling.
    private static string RealPath(string path) => new DirectoryInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName
        ?? (path.StartsWith("/var/", StringComparison.Ordinal) && Directory.Exists("/private" + path) ? "/private" + path : path);
}
