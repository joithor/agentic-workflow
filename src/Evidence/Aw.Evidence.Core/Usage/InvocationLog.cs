using Aw.Evidence.Core.Scripts;

namespace Aw.Evidence.Core.Usage;

/// <summary>Collects a run's model invocations in order. Not thread-safe: one run, one log.</summary>
public sealed class InvocationLog
{
    private readonly List<ModelInvocation> _all = [];

    /// <summary>Every invocation recorded so far.</summary>
    public IReadOnlyList<ModelInvocation> All => _all;

    /// <summary>Records one invocation.</summary>
    /// <param name="invocation">The invocation.</param>
    public void Record(ModelInvocation invocation) => _all.Add(invocation);

    /// <summary>Records the planner's invocation from the script's planning block.</summary>
    /// <param name="planning">The planning block.</param>
    public void RecordPlanning(PlanningMeta planning) =>
        Record(new(InvocationPhase.Planning, planning.Model, planning.ElapsedMs, Ok: true, planning.InputTokens, planning.OutputTokens, CacheHit: false));

    /// <summary>Times <paramref name="call"/> and records it; a null answer records as not ok.</summary>
    /// <param name="phase">Why the model is called.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="call">The call.</param>
    /// <param name="usage">What the answer reported about itself.</param>
    /// <typeparam name="T">The answer type.</typeparam>
    /// <returns>The answer, or null.</returns>
    public async ValueTask<T?> TimeAsync<T>(InvocationPhase phase, TimeProvider clock, Func<ValueTask<T?>> call, Func<T, InvocationUsage?> usage)
        where T : class
    {
        var start = clock.GetTimestamp();
        T? answer = null;
        try
        {
            answer = await call().ConfigureAwait(false);
        }
        finally
        {
            var reported = answer is null ? null : usage(answer);
            var elapsed = (long)clock.GetElapsedTime(start).TotalMilliseconds;
            Record(new(phase, reported?.Model, elapsed, answer is not null, reported?.InputTokens, reported?.OutputTokens, CacheHit: false));
        }

        return answer;
    }
}
