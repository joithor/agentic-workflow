namespace Aw.Sindri.Core.Sessions;

/// <summary>What the state merge looks at (collector spec §5.2).</summary>
/// <param name="Record">Claude's record, or null.</param>
/// <param name="Hook">The hook's state file, or null.</param>
/// <param name="TranscriptWrite">The transcript's modification time, or null.</param>
/// <param name="Now">The time of the pass.</param>
public sealed record StateInputs(ClaudeRecord? Record, FleetStateFile? Hook, DateTimeOffset? TranscriptWrite, DateTimeOffset Now);
