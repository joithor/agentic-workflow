namespace Aw.Sindri.Tests.Workflows;

/// <summary>The 'temporal' CLI pinned in binaries.lock, where scripts/binaries-lock.sh fetch installs it.</summary>
internal static class PinnedTemporal
{
    /// <summary>The pinned version, from binaries.lock.</summary>
    public static string Version => Row()[1];

    /// <summary>The installed binary; fails the test, naming the fetch command, when it isn't installed.</summary>
    /// <returns>Its path, wrapped so the server dies with this test host (<see cref="Spike.ServerGuard"/>).</returns>
    public static string Path() => Spike.ServerGuard.Wrap(Unwrapped());

    /// <summary>The installed binary itself, for the test that checks the guard; everything else starts <see cref="Path"/>.</summary>
    /// <returns>Its path.</returns>
    public static string Unwrapped()
    {
        var stateDir = Environment.GetEnvironmentVariable("AW_STATE_DIR") is { Length: > 0 } dir
            ? dir
            : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        var path = System.IO.Path.Combine(stateDir, "cache", "bin", $"temporal-{Version}", "temporal");
        if (!File.Exists(path))
        {
            Assert.Fail($"the pinned temporal CLI {Version} is not installed at {path}: run scripts/binaries-lock.sh fetch temporal");
        }

        return path;
    }

    private static string[] Row() =>
        File.ReadLines(System.IO.Path.Combine(RepoRoot(), "binaries.lock"))
            .Select(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .First(f => f.Length == 6 && f[0] == "temporal");

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No AgenticWorkflow.slnx above the test's base directory.");
    }
}
