using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IProcessLocations contract: the same tests run against the in-memory fake and the real scanner.</summary>
public abstract class ProcessLocationsContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>The scanner under test.</summary>
    /// <returns>The scanner.</returns>
    protected abstract IProcessLocations Create();

    /// <summary>Starts a long-running process whose working directory is <paramref name="directory"/>.</summary>
    /// <param name="directory">An existing directory.</param>
    /// <returns>Its pid.</returns>
    protected abstract int StartProcessIn(string directory);

    /// <summary>A directory that exists for the test.</summary>
    /// <returns>Its path, resolved (no symlinks).</returns>
    protected abstract string NewDirectory();

    /// <summary>A process started in a directory is listed with that directory among its paths.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_process_started_in_a_directory_holds_that_directory()
    {
        var directory = NewDirectory();
        var pid = StartProcessIn(directory);

        var scan = await Create().Locations(_none);

        Expect.That(scan.IsSuccess, "the scan runs");
        var listed = scan.Value.FirstOrDefault(p => p.Pid == pid);
        Expect.That(listed is not null && listed.Paths.Any(path => path == directory), "the process is listed with its working directory");
        Expect.That(listed is not null && listed.Command.Contains("sleep", StringComparison.Ordinal), "and its command line");
    }

    /// <summary>A directory nothing was started in is held by no process.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_directory_nothing_runs_in_is_held_by_nobody()
    {
        var busy = NewDirectory();
        var idle = NewDirectory();
        _ = StartProcessIn(busy);

        var scan = await Create().Locations(_none);

        Expect.That(scan.IsSuccess, "the scan runs");
        Expect.That(!scan.Value.Any(p => p.Paths.Any(path => path == idle || path.StartsWith(idle + "/", StringComparison.Ordinal))), "no process holds the idle directory");
    }
}
