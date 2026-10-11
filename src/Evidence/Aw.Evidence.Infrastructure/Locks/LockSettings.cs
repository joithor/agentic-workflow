namespace Aw.Evidence.Infrastructure.Locks;

/// <summary>Where the machine locks live, how many heavy-job slots there are, and whether a parent already holds one.</summary>
/// <param name="QaStack">The QA-stack lock directory: AW_QA_STACK_LOCK, or &lt;state root&gt;/locks/qa-stack.lock. One slot.</param>
/// <param name="HeavyJob">The heavy-job lock's slot 1: AW_HEAVY_JOB_LOCK, or &lt;state root&gt;/locks/heavy-job.lock; slot k is '&lt;HeavyJob&gt;.k'.</param>
/// <param name="HeavyJobHeld">AW_HEAVY_LOCK_HELD=1: a parent process (the heavy wrapper) already holds a heavy-job slot.</param>
/// <param name="HeavyJobSlots">AW_HEAVY_SLOTS: how many heavy jobs may run at once (default 2).</param>
/// <param name="IgnoredHeavyJobSlots">An AW_HEAVY_SLOTS value that isn't a whole number of at least 1 (the default is used instead), or null.</param>
internal sealed record LockSettings(string QaStack, string HeavyJob, bool HeavyJobHeld, int HeavyJobSlots, string? IgnoredHeavyJobSlots);
