using System.Diagnostics;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Tests.Decisions;

/// <summary>The SQLite prune reads before it writes: with nothing to drop it never takes the write lock.</summary>
[Lane(TestLane.Integration)]
public sealed class SqlitePruneTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact]
    public async Task Pruning_with_nothing_old_enough_answers_at_once_while_another_writer_holds_the_lock()
    {
        var ct = TestContext.Current.CancellationToken;
        var log = _state.DecisionLog();
        Assert.True((await log.Record(A.Decision().At(KnownInstants.Epoch).Build(), ct)).IsSuccess);
        await using var writer = new SqliteConnection($"Data Source={Path.Combine(_state.Path, "judge", "decisions.sqlite")}");
        await writer.OpenAsync(ct);
        await using (var begin = writer.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE";
            _ = await begin.ExecuteNonQueryAsync(ct);
        }

        var clock = Stopwatch.StartNew();
        var pruned = await log.PruneDetailsBefore(KnownInstants.Epoch.AddDays(-1), ct);

        Assert.Equal(0, pruned.Value);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"no wait for the busy timeout, took {clock.Elapsed}");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _state.Dispose();
    }
}
