using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Engine P0 pass line 1 (engine spec rev 8, Phases "Phase 0"): the dev server starts with 'sindri serve', loopback only,
/// its database under the Sindri home. Runs the real pinned 'temporal' CLI (binaries.lock); without it the test fails and
/// says how to install it, so a missing binary never reads as a pass.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalDevServerTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact]
    public async Task Serve_starts_the_dev_server_on_loopback_with_its_database_under_the_home()
    {
        var temporal = PinnedTemporal();
        var server = new TemporalDevServer();

        var running = await server.StartAsync(new TemporalServerStart(temporal, _home.Path), TestContext.Current.CancellationToken);
        try
        {
            Assert.True(IPAddress.IsLoopback(running.Frontend.Address), $"frontend {running.Frontend} is not loopback");
            Assert.Equal(Path.Combine(_home.Path, SindriTemporal.DatabaseFile), running.DatabasePath);
            Assert.True(File.Exists(running.DatabasePath), $"no database at {running.DatabasePath}");
            Assert.True(running.ProcessId > 0);
        }
        finally
        {
            await server.StopAsync(running, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>The 'temporal' CLI pinned in binaries.lock, where scripts/binaries-lock.sh fetch installs it.</summary>
    private static string PinnedTemporal()
    {
        var root = RepoRoot();
        var row = File.ReadLines(Path.Combine(root, "binaries.lock"))
            .Select(l => l.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .First(f => f.Length == 6 && f[0] == "temporal");
        var stateDir = Environment.GetEnvironmentVariable("AW_STATE_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow");
        var path = Path.Combine(stateDir, "cache", "bin", $"temporal-{row[1]}", "temporal");
        if (!File.Exists(path))
        {
            Assert.Fail($"the pinned temporal CLI {row[1]} is not installed at {path}: run scripts/binaries-lock.sh fetch temporal");
        }

        return path;
    }

    private static string RepoRoot()
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
}
