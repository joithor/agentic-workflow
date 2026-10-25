using Aw.Context.Infrastructure.Extract;

namespace Aw.Context.Tests.Infrastructure;

[Trait("Lane", "integration")]
public sealed class GraphifyProcessTests
{
    private static readonly Dictionary<string, string> _env = new(StringComparer.Ordinal) { ["PATH"] = "/usr/bin:/bin" };

    [Fact]
    public async Task A_tool_that_prints_megabytes_is_drained_and_only_the_first_64_KB_is_kept()
    {
        var ran = await GraphifyProcess.Spawn(["/bin/sh", "-c", "head -c 3000000 /dev/zero | tr '\\0' a; head -c 3000000 /dev/zero | tr '\\0' b >&2"], _env, Path.GetTempPath(), TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        Assert.NotNull(ran);
        Assert.Equal(0, ran.Exit);
        Assert.Equal(GraphifyProcess.MaxKept, ran.Stdout.Length);
        Assert.All(ran.Stdout, c => Assert.Equal('a', c));
    }

    [Fact]
    public async Task A_run_over_its_time_is_killed_and_reported_as_timed_out()
    {
        var ran = await GraphifyProcess.Spawn(["/bin/sh", "-c", "sleep 30"], _env, Path.GetTempPath(), TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);

        Assert.True(ran is { TimedOut: true });
    }
}
