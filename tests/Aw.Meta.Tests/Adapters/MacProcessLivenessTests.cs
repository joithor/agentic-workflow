using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Adapters;

/// <summary>MacProcessLiveness through a fake command runner and clock, so it runs on any OS.</summary>
public sealed class MacProcessLivenessTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static (MacProcessLiveness Liveness, InMemoryCommandRunner Runner) Make(Func<InMemoryCommandRunner, InMemoryCommandRunner> script)
    {
        var runner = script(new InMemoryCommandRunner());
        var clock = new FakeTimeProvider(_now);
        return (new MacProcessLiveness(new MacHostReader("/state", runner, MacToolPaths.Resolve(_ => false), _ => 0), clock), runner);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_process_that_started_before_the_file_was_written_is_alive_and_ps_is_asked_for_that_pid()
    {
        var (liveness, runner) = Make(r => r.When("ps -o etime= -p 321", "   02:00:00\n"));

        var alive = await liveness.IsRunning(321, _now - TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        Assert.True(alive.Value);
        Assert.Equal(["ps -o etime= -p 321"], runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_process_that_started_after_the_file_was_written_is_a_reused_pid()
    {
        var (liveness, _) = Make(r => r.When("ps -o etime= -p 321", "   00:30\n"));

        var alive = await liveness.IsRunning(321, _now - TimeSpan.FromHours(1), TestContext.Current.CancellationToken);

        Assert.True(alive.IsSuccess);
        Assert.False(alive.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("01:05:00")]
    [InlineData("3-04:05:06")]
    public async Task The_elapsed_time_forms_ps_prints_are_read(string etime)
    {
        var (liveness, _) = Make(r => r.When("etime=", etime + "\n"));

        var alive = await liveness.IsRunning(5, _now, TestContext.Current.CancellationToken);

        Assert.True(alive.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pid_ps_does_not_list_is_not_alive()
    {
        var (liveness, _) = Make(r => r.When("etime=", "", exitCode: 1));

        var alive = await liveness.IsRunning(321, _now, TestContext.Current.CancellationToken);

        Assert.True(alive.IsSuccess);
        Assert.False(alive.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task A_non_pid_is_not_alive_and_ps_is_not_run(int pid)
    {
        var (liveness, runner) = Make(r => r);

        var alive = await liveness.IsRunning(pid, _now, TestContext.Current.CancellationToken);

        Assert.False(alive.Value);
        Assert.Empty(runner.Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ps_that_cannot_run_is_AW_META_010_not_a_guess()
    {
        var (liveness, _) = Make(r => r);

        var alive = await liveness.IsRunning(321, _now, TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, alive.Error.Code);
        Assert.Contains("unavailable on macOS: ps could not be run", alive.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_elapsed_time_that_cannot_be_read_is_AW_META_010()
    {
        var (liveness, _) = Make(r => r.When("etime=", "soon\n"));

        var alive = await liveness.IsRunning(321, _now, TestContext.Current.CancellationToken);

        Assert.Equal(MetaErrors.HostUnreadable.Code, alive.Error.Code);
    }
}
