using Aw.Sindri.Core.Sessions;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// What every <see cref="IProcessTable"/> keeps: each pid once and in order, an environment reduced to the allowlist (a planted secret never comes back), and
/// nothing for an unknown pid. The fake and the /proc adapter run it; the fixture holds a process whose environment has a planted token.
/// </summary>
public abstract class ProcessTableContract
{
    /// <summary>The planted fake secret every table's environment holds under a name off the allowlist.</summary>
    public const string PlantedToken = "sk-ant-" + "FAKE-must-never-appear";

    /// <summary>A table with at least one process whose environment holds <see cref="PlantedToken"/> and one allowlisted name.</summary>
    /// <returns>The table.</returns>
    protected abstract IProcessTable Create();

    [Fact]
    public void Read_lists_each_pid_once_in_ascending_order()
    {
        var pids = Create().Read(CancellationToken.None).Value.Select(static p => p.Pid).ToList();

        Expect.True(pids.Count > 0);
        Expect.Sequence(pids.Distinct().Order(), pids);
    }

    [Fact]
    public void Environment_never_returns_a_name_off_the_allowlist()
    {
        var subject = Create();

        var environments = subject.Read(CancellationToken.None).Value.Select(p => subject.Environment(p.Pid)).ToList();

        Expect.True(environments.TrueForAll(static e => e.Keys.All(EnvironmentAllowlist.Names.Contains)));
        Expect.True(!environments.Exists(static e => e.Values.Contains(PlantedToken, StringComparer.Ordinal)));
        Expect.True(environments.Exists(static e => e.Count > 0));
    }

    [Fact]
    public void An_unknown_pid_has_no_environment_cwd_or_files()
    {
        var subject = Create();

        Expect.Equal(0, subject.Environment(2_000_000_000).Count);
        Expect.True(subject.Cwd(2_000_000_000) is null);
        Expect.Equal(0, subject.OpenFiles(2_000_000_000).Count);
    }
}
