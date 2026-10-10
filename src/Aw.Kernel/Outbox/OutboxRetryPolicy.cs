namespace Aw.Kernel.Outbox;

/// <summary>
/// When a failed outbox command is tried again: the delay doubles from <see cref="BaseDelay"/> up to
/// <see cref="MaxDelay"/>, and after <see cref="MaxAttempts"/> failures the command is dead-lettered (kept for
/// inspection, never read as pending again). No jitter, so retries are deterministic under a fake clock.
/// </summary>
public sealed record OutboxRetryPolicy
{
    private OutboxRetryPolicy(int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay)
    {
        MaxAttempts = maxAttempts;
        BaseDelay = baseDelay;
        MaxDelay = maxDelay;
    }

    /// <summary>8 attempts, 30 seconds doubling to at most an hour.</summary>
    public static OutboxRetryPolicy Default { get; } = new(8, TimeSpan.FromSeconds(30), TimeSpan.FromHours(1));

    /// <summary>The number of failed attempts after which the command is dead-lettered.</summary>
    public int MaxAttempts { get; }

    /// <summary>The delay after the first failure.</summary>
    public TimeSpan BaseDelay { get; }

    /// <summary>The longest delay between attempts.</summary>
    public TimeSpan MaxDelay { get; }

    /// <summary>Builds a policy, checking its values.</summary>
    /// <param name="maxAttempts">At least 1.</param>
    /// <param name="baseDelay">More than zero.</param>
    /// <param name="maxDelay">At least <paramref name="baseDelay"/>.</param>
    /// <returns>The policy, or AW-KERNEL-001 when a value is out of range.</returns>
    public static Result<OutboxRetryPolicy> Create(int maxAttempts, TimeSpan baseDelay, TimeSpan maxDelay) =>
        maxAttempts >= 1 && baseDelay > TimeSpan.Zero && maxDelay >= baseDelay
            ? new OutboxRetryPolicy(maxAttempts, baseDelay, maxDelay)
            : KernelErrors.Invalid.With("An outbox retry policy needs maxAttempts >= 1 and 0 < baseDelay <= maxDelay.");

    /// <summary>When to try again after a failure.</summary>
    /// <param name="failedAttempts">Failures so far, including this one (1 or more; 0 is a bug and throws).</param>
    /// <param name="failedAt">When this attempt failed.</param>
    /// <returns>The next attempt time, or null when the command is now dead-lettered.</returns>
    public DateTimeOffset? NextAttempt(int failedAttempts, DateTimeOffset failedAt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttempts, 1);
        if (failedAttempts >= MaxAttempts)
        {
            return null;
        }

        var ticks = BaseDelay.Ticks * Math.Pow(2, Math.Min(failedAttempts - 1, 62));
        return failedAt + (ticks >= MaxDelay.Ticks ? MaxDelay : TimeSpan.FromTicks((long)ticks));
    }
}
