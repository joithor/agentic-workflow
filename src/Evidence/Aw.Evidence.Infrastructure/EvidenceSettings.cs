namespace Aw.Evidence.Infrastructure;

/// <summary>Evidence's infrastructure settings, read from the environment (SettingsFromEnvironment).</summary>
/// <param name="StateDirectory">Where Evidence keeps its state: $AW_STATE_DIR/evidence, or ~/.agentic-workflow/evidence.</param>
/// <param name="QaStackLock">The QA-stack lock directory: AW_QA_STACK_LOCK, or &lt;state root&gt;/locks/qa-stack.lock.</param>
/// <param name="HeavyJobLock">The heavy-job lock directory: AW_HEAVY_JOB_LOCK, or &lt;state root&gt;/locks/heavy-job.lock.</param>
/// <param name="HeavyLockHeld">AW_HEAVY_LOCK_HELD=1: a parent process (the heavy wrapper) already holds the heavy-job lock.</param>
/// <remarks>The UI_EVIDENCE_* values (user agent, app title, seed policy) are read per use through ISecretsSource.</remarks>
internal sealed record EvidenceSettings(string StateDirectory, string QaStackLock, string HeavyJobLock, bool HeavyLockHeld);
