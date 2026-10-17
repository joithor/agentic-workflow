using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Tests.Server;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Per-source timeouts in a pass (collector spec §4.1): who stays live, what clears, what is never started twice.</summary>
public sealed class CollectorSourceTimeoutTests
{
    private static readonly TimeSpan _budget = TimeSpan.FromMilliseconds(250);

    private static Dictionary<string, string> Env(params (string, string)[] pairs) => pairs.ToDictionary(static p => p.Item1, static p => p.Item2, StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task One_stuck_pids_environment_leaves_the_other_pids_and_a_stuck_judge_child_is_not_published()
    {
        var fake = new Testing.FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        _ = fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/a").With(Proc(11, 1, "claude", argv0: "claude", ticks: 101), cwd: "/w/b")
            .With(Proc(12, 1, "claude", argv0: "claude", ticks: 102), Env(("AW_JUDGE_CHILD", "1")));
        _ = await c.Pass(_budget);
        _ = await c.After(TimeSpan.FromSeconds(2));
        var pendingBefore = c.Memory.Pending.Count;

        table.StickEnvironment(10);
        table.StickEnvironment(12);
        var response = await c.After(TimeSpan.FromSeconds(2));
        table.Release();

        Assert.True(response.Good);
        Assert.Equal("claude-code/pid-10-100,claude-code/pid-11-101", string.Join(',', c.Snapshot.Current!.Sessions.Select(static s => s.Ref.ToString())));
        Assert.Equal(pendingBefore, c.Memory.Pending.Count);   // no 'gone' for 10, no 'appeared' for 12
        var problem = Assert.Single(c.Snapshot.Current.Problems);
        Assert.Equal(("environment", "SND-SESSION-001"), (problem.Source, problem.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_released_source_is_read_again_and_its_problem_row_clears()
    {
        using var blocked = new BlockingSources();
        var c = new CollectorHarness(records: blocked, fleet: blocked);
        _ = c.Fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/api");
        _ = await c.Pass(_budget);
        Assert.Equal(2, c.Snapshot.Current!.Problems.Count);

        blocked.Release();
        for (var tries = 0; tries < 200 && c.Snapshot.Current.Problems.Count > 0; tries++)
        {
            _ = await c.Pass(_budget);
        }

        Assert.Empty(c.Snapshot.Current.Problems);
        Assert.Equal((2, 2), (blocked.RecordReads, blocked.FleetReads));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_given_up_on_has_its_read_cancelled()
    {
        using var blocked = new BlockingSources();
        var c = new CollectorHarness(records: blocked, fleet: blocked);

        _ = await c.Pass(_budget);
        var cancelled = blocked.RecordToken.IsCancellationRequested;
        blocked.Release();

        Assert.True(cancelled);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_process_table_that_overruns_twice_keeps_the_previous_snapshot_and_is_not_read_again()
    {
        var fake = new Testing.FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        _ = fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/api");
        _ = await c.Pass(_budget);
        table.BlockReads = true;

        var first = await c.Pass(TimeSpan.FromMilliseconds(50));
        var second = await c.Pass(TimeSpan.FromMilliseconds(50));
        table.Release();

        Assert.Equal((true, true), (first.Overran, second.Overran));
        Assert.Equal(2, table.Reads);   // the good pass and the first overrun; the second didn't start another
        _ = Assert.Single(c.Snapshot.Current!.Sessions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_full_pending_queue_drops_the_oldest_and_logs_once()
    {
        var log = new ListLogger<CollectSessionsService>();
        var c = new CollectorHarness(logger: log);
        var old = new FleetChange(new SessionRef(AgentId.From("claude-code"), SessionId.From("aaaaaaaa-0000-4000-8000-0000000000ff")), FleetChangeKind.Gone, SessionState.Running, c.Now);
        c.Memory.Pending = [.. Enumerable.Repeat(old, CollectorMemory.PendingCap)];
        _ = c.Fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/api");

        _ = await c.Pass(_budget);
        _ = await c.After(TimeSpan.FromSeconds(2));   // the appeared change lands: over the cap
        _ = await c.After(TimeSpan.FromSeconds(2));

        Assert.Equal(CollectorMemory.PendingCap, c.Memory.Pending.Count);
        Assert.Equal(FleetChangeKind.Appeared, c.Memory.Pending[^1].Kind);
        Assert.Equal(1, log.Lines.Count(static l => l.Contains("fleet changes the ledger hasn't taken", StringComparison.Ordinal)));
    }
}
