using Aw.Evidence.Infrastructure;

namespace Aw.Evidence.Tests.Infrastructure;

public sealed class SettingsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Locks_and_state_live_under_the_state_root()
    {
        var settings = SettingsFromEnvironment.Read(Env(("AW_STATE_DIR", "/state"), ("HOME", "/home/u")));

        Assert.Equal("/state/evidence", settings.StateDirectory);
        Assert.Equal("/state/locks/qa-stack.lock", settings.QaStackLock);
        Assert.Equal("/state/locks/heavy-job.lock", settings.HeavyJobLock);
        Assert.False(settings.HeavyLockHeld);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_state_root_the_home_directory_holds_it()
    {
        var settings = SettingsFromEnvironment.Read(Env(("HOME", "/home/u")));

        Assert.Equal("/home/u/.agentic-workflow/locks/heavy-job.lock", settings.HeavyJobLock);
    }

    [Fact, Trait("Lane", "unit")]
    public void Explicit_lock_paths_and_the_held_flag_win()
    {
        var settings = SettingsFromEnvironment.Read(Env(
            ("AW_STATE_DIR", "/state"), ("AW_QA_STACK_LOCK", "/tmp/q.lock"), ("AW_HEAVY_JOB_LOCK", "/tmp/h.lock"), ("AW_HEAVY_LOCK_HELD", "1")));

        Assert.Equal("/tmp/q.lock", settings.QaStackLock);
        Assert.Equal("/tmp/h.lock", settings.HeavyJobLock);
        Assert.True(settings.HeavyLockHeld);
    }

    private static Func<string, string?> Env(params (string Name, string Value)[] values) =>
        name => values.FirstOrDefault(v => v.Name == name).Value;
}
