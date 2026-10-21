using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>A block-runtime Step whose run is whatever the test gives it, counting its runs.</summary>
/// <param name="run">What a run does.</param>
public sealed class FakeBlockStep(Func<StepContext, CancellationToken, ValueTask<BlockStepResult>> run) : IBlockStep
{
    private int _runs;
    private int _cancelled;
    private StepContext? _last;

    /// <summary>How many runs started.</summary>
    public int Runs => Volatile.Read(ref _runs);

    /// <summary>The context of the last run.</summary>
    public StepContext? Last => Volatile.Read(ref _last);

    /// <summary>How many runs were cancelled.</summary>
    public int Cancelled => Volatile.Read(ref _cancelled);

    /// <summary>A Step that reports <paramref name="outcome"/> with a 'fact:decision'.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The Step.</returns>
    public static FakeBlockStep Completing(string outcome) => new((_, _) => ValueTask.FromResult(Result(outcome)));

    /// <summary>A Step whose first run hangs until it is cancelled and whose later runs report <paramref name="outcome"/>.</summary>
    /// <param name="outcome">The outcome of every run after the first.</param>
    /// <returns>The Step.</returns>
    public static FakeBlockStep HangingThenCompleting(string outcome)
    {
        var started = 0;
        return new(async (_, ct) =>
        {
            if (Interlocked.Increment(ref started) == 1)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            }

            return Result(outcome);
        });
    }

    /// <summary>A Step that returns <see cref="BlockStepResult.Failed"/> (it does not throw).</summary>
    /// <param name="reason">The failure.</param>
    /// <returns>The Step.</returns>
    public static FakeBlockStep Failing(string reason) => new((_, _) => ValueTask.FromResult(BlockStepResult.Failed(reason)));

    /// <summary>A Step that waits for <paramref name="gate"/> (or its own cancellation) and then reports <paramref name="outcome"/>.</summary>
    /// <param name="gate">What the Step waits for.</param>
    /// <param name="outcome">The outcome once the gate is done.</param>
    /// <returns>The Step.</returns>
    public static FakeBlockStep Gated(Task gate, string outcome) => new(async (_, ct) =>
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        return Result(outcome);
    });

    /// <summary>A Step that never returns until it is cancelled.</summary>
    /// <returns>The Step.</returns>
    public static FakeBlockStep Hanging() => new(static async (_, ct) =>
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
        return BlockStepResult.Failed("unreachable");
    });

    private static BlockStepResult Result(string outcome) => BlockStepResult.Completed(
        OutcomeName.From(outcome),
        new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:decision")] = JsonSerializer.SerializeToElement(new { choice = outcome, provider = "fake", decisionId = "JD-1" }) });

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _runs);
        Volatile.Write(ref _last, context);
        try
        {
            return await run(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ = Interlocked.Increment(ref _cancelled);
            throw;
        }
    }
}
