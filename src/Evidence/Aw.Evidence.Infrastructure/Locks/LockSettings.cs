namespace Aw.Evidence.Infrastructure.Locks;

/// <summary>Where the machine locks live and whether a parent already holds the heavy-job lock.</summary>
/// <param name="QaStack">The QA-stack lock directory: AW_QA_STACK_LOCK, or &lt;state root&gt;/locks/qa-stack.lock.</param>
/// <param name="HeavyJob">The heavy-job lock directory: AW_HEAVY_JOB_LOCK, or &lt;state root&gt;/locks/heavy-job.lock.</param>
/// <param name="HeavyJobHeld">AW_HEAVY_LOCK_HELD=1: a parent process (the heavy wrapper) already holds the heavy-job lock.</param>
internal sealed record LockSettings(string QaStack, string HeavyJob, bool HeavyJobHeld);
