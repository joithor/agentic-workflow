namespace Aw.Sindri.Core.Sessions;

/// <summary>One state change of a session in this Sindri run (collector spec CA-2's stateHistory).</summary>
/// <param name="State">The state it changed to.</param>
/// <param name="Reason">Why it is asking, when it is.</param>
/// <param name="At">When it changed, as far as the source says.</param>
/// <param name="Source">Where the state came from.</param>
public sealed record SessionStateEntry(SessionState State, AskingReason? Reason, DateTimeOffset At, StateSource Source);
