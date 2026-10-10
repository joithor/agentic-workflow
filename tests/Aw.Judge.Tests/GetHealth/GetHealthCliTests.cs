using Aw.Judge.Core.Decisions;
using Aw.Judge.Infrastructure.Sqlite;

namespace Aw.Judge.Tests.GetHealth;

// The frozen surface (TS cli.ts runHealth), on a real decisions.sqlite.
public sealed class GetHealthCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Health_prints_ok_and_no_failures_on_a_new_state_directory()
    {
        var run = await new JudgeCli(_state).Run("health", "ignored-extra-argument");

        Assert.Equal(new CliRun(0, "{\"status\":\"ok\",\"failures24h\":0}\n", ""), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Health_prints_degraded_with_the_failure_count()
    {
        var cli = new JudgeCli(_state);
        var log = new SqliteDecisionLog(_state.Database());
        _ = await log.RecordFailure(new FailureRecord(cli.Clock.GetUtcNow().AddHours(-1), "wake-gate", "claude-cli", "timeout"), CancellationToken.None);

        var run = await cli.Run("health");

        Assert.Equal(new CliRun(0, "{\"status\":\"degraded\",\"failures24h\":1}\n", ""), run);
    }

    public void Dispose() => _state.Dispose();
}
