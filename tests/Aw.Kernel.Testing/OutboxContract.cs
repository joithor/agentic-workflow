using System.Globalization;
using Aw.Kernel.Outbox;
using Xunit;

namespace Aw.Kernel.Testing;

/// <summary>
/// The contract every <see cref="IOutbox"/> honours, run against the in-memory fake (<see cref="InMemoryOutbox"/>) and
/// each real adapter alike (spec §4b.5, csharp.md Seams). A derived test class binds <see cref="CreateOutbox"/> to one
/// implementation and carries its lane: '[Lane(TestLane.Unit)]' for the fake, '[Lane(TestLane.Integration)]' for SQL.
/// The contract owns the clock: it starts at <see cref="KnownInstants.Epoch"/> and moves only when a test advances it,
/// and a command is due when its next attempt time is at or before the clock's time.
/// </summary>
public abstract class OutboxContract
{
    private readonly ContractClock _clock = new();

    /// <summary>The retry policy the contract checks against: 3 attempts, 30 s doubling to at most 2 minutes.</summary>
    protected static OutboxRetryPolicy Policy { get; } =
        OutboxRetryPolicy.Create(3, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)).Value;

    /// <summary>An outbox over a fresh, empty store that reads time from <paramref name="clock"/> and retries with <paramref name="policy"/>.</summary>
    /// <param name="clock">The contract's clock; the implementation's only source of time.</param>
    /// <param name="policy">The retry policy.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The outbox.</returns>
    protected abstract ValueTask<IOutbox> CreateOutbox(TimeProvider clock, OutboxRetryPolicy policy, CancellationToken cancellationToken);

    /// <summary>A recorded command comes back from 'ReadPending' with every field as it was recorded.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_recorded_command_is_read_back_with_every_field()
    {
        const string test = nameof(A_recorded_command_is_read_back_with_every_field);
        var outbox = await Create(test);
        var command = Command(1, KnownInstants.Epoch) with { CausationId = new Guid(a: 0xc1, b: 0, c: 0, d: 0, e: 0, f: 0, g: 0, h: 0, i: 0, j: 0, k: 0), TraceId = "trace-1" };
        var bare = Command(2, KnownInstants.Epoch.AddSeconds(1));
        await Enqueue(test, outbox, command);
        await Enqueue(test, outbox, bare);

        var pending = await ReadPending(test, outbox, 10);

        if (pending.Count != 2 || pending[0] != command || pending[1] != bare)
        {
            throw Violation(test, $"expected exactly the two recorded commands, field for field; got {Describe(pending)}.");
        }
    }

    /// <summary>Recording an id twice is a conflict (AW-KERNEL-003), and the first command is kept.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Recording_an_id_twice_is_a_conflict()
    {
        const string test = nameof(Recording_an_id_twice_is_a_conflict);
        var outbox = await Create(test);
        var first = Command(1, KnownInstants.Epoch);
        await Enqueue(test, outbox, first);

        var again = await Checked(test, "Enqueue (same id)", () => outbox.Enqueue(first with { Kind = "other.kind" }, Token));

        ExpectError(test, "Enqueue of an id already recorded", again, KernelErrors.Conflict);
        var pending = await ReadPending(test, outbox, 10);
        if (pending.Count != 1 || pending[0] != first)
        {
            throw Violation(test, $"the refused duplicate changed the outbox; got {Describe(pending)}.");
        }
    }

    /// <summary>'ReadPending' returns due commands oldest first, by enqueue time, and at most 'max' of them.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task ReadPending_returns_the_oldest_first_up_to_max()
    {
        const string test = nameof(ReadPending_returns_the_oldest_first_up_to_max);
        var outbox = await Create(test);
        var newest = Command(1, KnownInstants.Epoch.AddSeconds(2));
        var oldest = Command(2, KnownInstants.Epoch);
        var middle = Command(3, KnownInstants.Epoch.AddSeconds(1));
        foreach (var command in new[] { newest, oldest, middle })
        {
            await Enqueue(test, outbox, command);
        }

        var two = await ReadPending(test, outbox, 2);

        if (two.Count != 2 || two[0].Id != oldest.Id || two[1].Id != middle.Id)
        {
            throw Violation(test, $"ReadPending(2) must return the two oldest by EnqueuedAt ({oldest.Id}, {middle.Id}); got {Describe(two)}.");
        }
    }

    /// <summary>'ReadPending' with a max below 1 is invalid (AW-KERNEL-001).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task ReadPending_with_max_below_one_is_invalid()
    {
        const string test = nameof(ReadPending_with_max_below_one_is_invalid);
        var outbox = await Create(test);

        var read = await Checked(test, "ReadPending(0)", () => outbox.ReadPending(0, Token));

        ExpectError(test, "ReadPending(0)", read, KernelErrors.Invalid);
    }

    /// <summary>A dispatched command is no longer pending, and dispatching it again is not found (AW-KERNEL-002).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_dispatched_command_is_no_longer_pending()
    {
        const string test = nameof(A_dispatched_command_is_no_longer_pending);
        var outbox = await Create(test);
        var sent = Command(1, KnownInstants.Epoch);
        var waiting = Command(2, KnownInstants.Epoch.AddSeconds(1));
        await Enqueue(test, outbox, sent);
        await Enqueue(test, outbox, waiting);

        await ExpectOk(test, "MarkDispatched", () => outbox.MarkDispatched(sent.Id, _clock.GetUtcNow(), Token));

        var pending = await ReadPending(test, outbox, 10);
        if (pending.Count != 1 || pending[0].Id != waiting.Id)
        {
            throw Violation(test, $"after MarkDispatched({sent.Id}) only {waiting.Id} may be pending; got {Describe(pending)}.");
        }

        var again = await Checked(test, "MarkDispatched (again)", () => outbox.MarkDispatched(sent.Id, _clock.GetUtcNow(), Token));
        ExpectError(test, "MarkDispatched of a command already dispatched", again, KernelErrors.NotFound);
    }

    /// <summary>Marking an id nobody recorded is not found (AW-KERNEL-002), for both marks.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Marking_an_unknown_id_is_not_found()
    {
        const string test = nameof(Marking_an_unknown_id_is_not_found);
        var outbox = await Create(test);
        await Enqueue(test, outbox, Command(1, KnownInstants.Epoch));
        var unknown = IdOf(99);

        var dispatched = await Checked(test, "MarkDispatched (unknown)", () => outbox.MarkDispatched(unknown, _clock.GetUtcNow(), Token));
        var failed = await Checked(test, "MarkFailed (unknown)", () => outbox.MarkFailed(unknown, SendError, Token));

        ExpectError(test, "MarkDispatched of an unknown id", dispatched, KernelErrors.NotFound);
        ExpectError(test, "MarkFailed of an unknown id", failed, KernelErrors.NotFound);
    }

    /// <summary>A failed command is held back until the policy's next attempt time, then is due again; each failure doubles the delay.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failed_command_waits_for_its_next_attempt()
    {
        const string test = nameof(A_failed_command_waits_for_its_next_attempt);
        var outbox = await Create(test);
        var command = Command(1, KnownInstants.Epoch);
        await Enqueue(test, outbox, command);

        await ExpectOk(test, "MarkFailed (1st)", () => outbox.MarkFailed(command.Id, SendError, Token));
        await ExpectPending(test, outbox, "29 s after the first failure", TimeSpan.FromSeconds(29), expected: false);
        await ExpectPending(test, outbox, "30 s after the first failure", TimeSpan.FromSeconds(1), expected: true);

        await ExpectOk(test, "MarkFailed (2nd)", () => outbox.MarkFailed(command.Id, SendError, Token));
        await ExpectPending(test, outbox, "59 s after the second failure", TimeSpan.FromSeconds(59), expected: false);
        await ExpectPending(test, outbox, "60 s after the second failure", TimeSpan.FromSeconds(1), expected: true);
    }

    /// <summary>When the policy gives up the command is dead-lettered: never pending again, and both marks are not found.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_command_is_dead_lettered_when_the_policy_gives_up()
    {
        const string test = nameof(A_command_is_dead_lettered_when_the_policy_gives_up);
        var outbox = await Create(test);
        var command = Command(1, KnownInstants.Epoch);
        await Enqueue(test, outbox, command);

        for (var attempt = 1; attempt <= Policy.MaxAttempts; attempt++)
        {
            var failed = await Checked(test, string.Create(CultureInfo.InvariantCulture, $"MarkFailed ({attempt})"), () => outbox.MarkFailed(command.Id, SendError, Token));
            if (failed.IsError)
            {
                throw Violation(test, string.Create(CultureInfo.InvariantCulture, $"MarkFailed attempt {attempt} of {Policy.MaxAttempts} returned {failed.Error.Code}; every attempt up to the last must succeed."));
            }

            _clock.Advance(Policy.MaxDelay);
        }

        _clock.Advance(TimeSpan.FromDays(1));
        if ((await ReadPending(test, outbox, 10)).Count != 0)
        {
            throw Violation(test, string.Create(CultureInfo.InvariantCulture, $"the command is still pending after {Policy.MaxAttempts} failures; it must be dead-lettered."));
        }

        ExpectError(test, "MarkDispatched of a dead-lettered command", await Checked(test, "MarkDispatched", () => outbox.MarkDispatched(command.Id, _clock.GetUtcNow(), Token)), KernelErrors.NotFound);
        ExpectError(test, "MarkFailed of a dead-lettered command", await Checked(test, "MarkFailed", () => outbox.MarkFailed(command.Id, SendError, Token)), KernelErrors.NotFound);
    }

    /// <summary>A failing command doesn't block the commands behind it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_failing_command_does_not_block_the_ones_behind_it()
    {
        const string test = nameof(A_failing_command_does_not_block_the_ones_behind_it);
        var outbox = await Create(test);
        var poison = Command(1, KnownInstants.Epoch);
        var next = Command(2, KnownInstants.Epoch.AddSeconds(1));
        await Enqueue(test, outbox, poison);
        await Enqueue(test, outbox, next);

        await ExpectOk(test, "MarkFailed", () => outbox.MarkFailed(poison.Id, SendError, Token));

        var pending = await ReadPending(test, outbox, 1);
        if (pending.Count != 1 || pending[0].Id != next.Id)
        {
            throw Violation(test, $"ReadPending(1) must skip the held-back {poison.Id} and return {next.Id}; got {Describe(pending)}.");
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Error SendError { get; } = KernelErrors.Unavailable.With("the receiver is down");

    private static Guid IdOf(int seed) => new(a: seed, b: 0, c: 0, d: 0, e: 0, f: 0, g: 0, h: 0, i: 0, j: 0, k: 0);

    private static OutboxCommand Command(int seed, DateTimeOffset enqueuedAt) =>
        new(IdOf(seed), "test.command", string.Create(CultureInfo.InvariantCulture, $$"""{"seed":{{seed}}}"""), OutboxCommand.CurrentSchemaVersion, enqueuedAt,
            "contract-host", "tenant-a", 0, new Guid(a: 0xc0de, b: 0, c: 0, d: 0, e: 0, f: 0, g: 0, h: 0, i: 0, j: 0, k: 0), CausationId: null, TraceId: null);

    private static string Describe(IReadOnlyList<OutboxCommand> commands) =>
        commands.Count == 0 ? "none" : string.Join("; ", commands);

    private static ContractViolationException Violation(string test, string detail, Exception? cause = null) =>
        cause is null
            ? new($"{nameof(OutboxContract)}.{test}: {detail}")
            : new($"{nameof(OutboxContract)}.{test}: {detail}", cause);

    private static async ValueTask<T> Checked<T>(string test, string call, Func<ValueTask<T>> run)
    {
        try
        {
            return await run();
        }
        catch (Exception e) when (e is not ContractViolationException)
        {
            throw Violation(test, $"{call} threw {e.GetType().Name}: {e.Message}", e);
        }
    }

    private static void ExpectError<T>(string test, string what, Result<T> result, ErrorDefinition expected)
    {
        if (result.IsSuccess)
        {
            throw Violation(test, $"{what} succeeded; it must return {expected.Code} ({expected.Kind}).");
        }

        if (result.Error.Code != expected.Code)
        {
            throw Violation(test, $"{what} returned {result.Error.Code} ({result.Error.Kind}); it must return {expected.Code} ({expected.Kind}).");
        }
    }

    private static async ValueTask ExpectOk(string test, string call, Func<ValueTask<Result<Unit>>> run)
    {
        var result = await Checked(test, call, run);
        if (result.IsError)
        {
            throw Violation(test, $"{call} returned {result.Error.Code} ({result.Error.Kind}): {result.Error.Message}");
        }
    }

    private static async ValueTask Enqueue(string test, IOutbox outbox, OutboxCommand command) =>
        await ExpectOk(test, $"Enqueue({command.Id})", () => outbox.Enqueue(command, Token));

    private static async ValueTask<IReadOnlyList<OutboxCommand>> ReadPending(string test, IOutbox outbox, int max)
    {
        var read = await Checked(test, string.Create(CultureInfo.InvariantCulture, $"ReadPending({max})"), () => outbox.ReadPending(max, Token));
        return read.IsSuccess
            ? read.Value
            : throw Violation(test, string.Create(CultureInfo.InvariantCulture, $"ReadPending({max}) returned {read.Error.Code} ({read.Error.Kind}): {read.Error.Message}"));
    }

    private async ValueTask<IOutbox> Create(string test) =>
        await Checked(test, "CreateOutbox", () => CreateOutbox(_clock, Policy, Token));

    private async ValueTask ExpectPending(string test, IOutbox outbox, string when, TimeSpan advance, bool expected)
    {
        _clock.Advance(advance);
        var pending = (await ReadPending(test, outbox, 10)).Count == 1;
        if (pending != expected)
        {
            throw Violation(test, string.Create(CultureInfo.InvariantCulture, $"{when} the command must {(expected ? string.Empty : "not ")}be pending (policy: {Policy.BaseDelay.TotalSeconds:0} s doubling)."));
        }
    }
}
