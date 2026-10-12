using Aw.Sindri.Core.Control;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>The contract every <see cref="IProcessAncestry"/> honours, run against the fake and the POSIX adapter.</summary>
public abstract class ProcessAncestryContract
{
    /// <summary>The ancestry under test.</summary>
    /// <returns>The ancestry.</returns>
    protected abstract IProcessAncestry Create();

    /// <summary>A pid with no running process behind it.</summary>
    /// <returns>The pid.</returns>
    protected abstract ValueTask<int> GonePid();

    /// <summary>This process's ancestors end at pid 1 (launchd or init), each with a name and a distinct pid.</summary>
    [Fact]
    public void Ancestors_of_this_process_end_at_pid_1_or_launchd()
    {
        var ancestors = Create().Ancestors(Environment.ProcessId, 64);

        Expect.True(ancestors.Count > 0);
        Expect.Equal(1, ancestors[^1].Pid);
        Expect.True(ancestors.All(a => a.Name.Length > 0));
        Expect.Equal(ancestors.Count, ancestors.Select(a => a.Pid).Distinct().Count());
    }

    /// <summary>The walk stops after max entries, nearest first.</summary>
    [Fact]
    public void The_walk_stops_after_max_entries_nearest_first()
    {
        var ancestry = Create();
        var all = ancestry.Ancestors(Environment.ProcessId, 64);

        var one = ancestry.Ancestors(Environment.ProcessId, 1);

        Expect.Sequence(all.Take(1), one);
    }

    /// <summary>A pid with nothing behind it has no ancestors: unreadable, not an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_gone_pid_has_no_ancestors()
    {
        var ancestry = Create();

        var ancestors = ancestry.Ancestors(await GonePid(), 64);

        Expect.Equal(0, ancestors.Count);
    }
}
