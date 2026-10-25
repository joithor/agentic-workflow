using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.SetupWatch;

namespace Aw.Sindri.Tests.Setup;

/// <summary>
/// The single-slot class of the setup service's automatic jobs (setup-once spec §5): one runs at a time, and when the 8-job limit is reached the next one waits and runs when a slot frees;
/// it never fails for that.
/// </summary>
[Trait("Lane", "unit")]
public sealed class SetupJobQueueTests
{
    private static readonly JobKind _mcp = JobKind.From("RegisterMcp");
    private static readonly JobKind _repo = JobKind.From("AddRepo");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private int _running;
    private readonly List<string> _started = [];

    private SetupJobQueue Queue() => new(() => _running);

    private Func<CancellationToken, ValueTask<Result<Unit>>> Start(string name) => _ =>
    {
        _started.Add(name);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    };

    [Fact]
    public async Task A_job_starts_at_once_when_the_slot_is_free_and_the_limit_is_not_reached()
    {
        var queue = Queue();

        var outcome = await queue.Submit(_mcp, JobKey.From("mcp-a"), Start("a"), Token);

        Assert.Equal(SetupJobOutcome.Started, outcome.Value);
        Assert.Equal(["a"], _started);
    }

    [Fact]
    public async Task A_second_job_waits_while_the_first_holds_the_slot_and_starts_in_order_when_it_finishes()
    {
        var queue = Queue();
        _ = await queue.Submit(_mcp, JobKey.From("mcp-a"), Start("a"), Token);

        var second = await queue.Submit(_mcp, JobKey.From("mcp-b"), Start("b"), Token);
        var third = await queue.Submit(_repo, JobKey.From("c"), Start("c"), Token);

        Assert.Equal((SetupJobOutcome.Queued, SetupJobOutcome.Queued), (second.Value, third.Value));
        Assert.Equal(["a"], _started);
        await queue.Finished(_mcp, JobKey.From("mcp-a"), Token);
        Assert.Equal(["a", "b"], _started);
        await queue.Finished(_mcp, JobKey.From("mcp-b"), Token);
        Assert.Equal(["a", "b", "c"], _started);
    }

    [Fact]
    public async Task At_the_job_limit_a_job_waits_instead_of_failing_and_runs_when_a_slot_frees()
    {
        _running = JobLimits.MaxRunning;
        var queue = Queue();

        var outcome = await queue.Submit(_mcp, JobKey.From("mcp-a"), Start("a"), Token);

        Assert.Equal(SetupJobOutcome.Queued, outcome.Value);
        Assert.Empty(_started);
        await queue.Pump(Token);
        Assert.Empty(_started);
        _running = JobLimits.MaxRunning - 1;
        await queue.Pump(Token);
        Assert.Equal(["a"], _started);
    }

    [Fact]
    public async Task The_same_job_is_never_queued_twice()
    {
        var queue = Queue();
        _ = await queue.Submit(_mcp, JobKey.From("mcp-a"), Start("a"), Token);
        _ = await queue.Submit(_mcp, JobKey.From("mcp-b"), Start("b"), Token);

        var again = await queue.Submit(_mcp, JobKey.From("mcp-b"), Start("b2"), Token);
        var running = await queue.Submit(_mcp, JobKey.From("mcp-a"), Start("a2"), Token);

        Assert.Equal((SetupJobOutcome.Duplicate, SetupJobOutcome.Duplicate), (again.Value, running.Value));
        Assert.Equal([(_mcp, JobKey.From("mcp-b"))], queue.Waiting);
    }

    [Fact]
    public async Task A_start_the_runner_refuses_for_the_limit_stays_at_the_head_and_is_not_a_failure()
    {
        var queue = Queue();
        static ValueTask<Result<Unit>> Refused(CancellationToken _) => ValueTask.FromResult<Result<Unit>>(SindriErrors.JobsRateLimited.With("8 jobs are running"));

        var outcome = await queue.Submit(_mcp, JobKey.From("mcp-a"), Refused, Token);
        _ = await queue.Submit(_repo, JobKey.From("c"), Start("c"), Token);

        Assert.Equal(SetupJobOutcome.Queued, outcome.Value);
        Assert.Equal([(_mcp, JobKey.From("mcp-a")), (_repo, JobKey.From("c"))], queue.Waiting);
        Assert.Empty(_started);
    }

    [Fact]
    public async Task Any_other_refusal_is_the_callers_error_and_frees_the_slot_for_the_next_job()
    {
        var queue = Queue();
        static ValueTask<Result<Unit>> Refused(CancellationToken _) => ValueTask.FromResult<Result<Unit>>(SindriErrors.InvalidInput.With("nope"));

        var outcome = await queue.Submit(_mcp, JobKey.From("mcp-a"), Refused, Token);
        var next = await queue.Submit(_mcp, JobKey.From("mcp-b"), Start("b"), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, outcome.Error.Code);
        Assert.Equal(SetupJobOutcome.Started, next.Value);
        Assert.Equal(["b"], _started);
    }

    [Fact]
    public async Task A_submit_cancelled_while_it_waits_for_the_queue_leaves_nothing_behind_to_start_later()
    {
        var queue = Queue();
        var gate = new TaskCompletionSource<Result<Unit>>();
        var first = queue.Submit(_mcp, JobKey.From("mcp-a"), _ => new ValueTask<Result<Unit>>(gate.Task), Token).AsTask();   // holds the turn while its start is pending
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var second = queue.Submit(_mcp, JobKey.From("mcp-b"), Start("b"), cancel.Token).AsTask();
        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        gate.SetResult(Unit.Value);
        _ = await first;
        await queue.Finished(_mcp, JobKey.From("mcp-a"), Token);
        var next = await queue.Submit(_mcp, JobKey.From("mcp-c"), Start("c"), Token);

        Assert.Equal(["c"], _started);
        Assert.Equal(SetupJobOutcome.Started, next.Value);
        Assert.Empty(queue.Waiting);
    }
}
