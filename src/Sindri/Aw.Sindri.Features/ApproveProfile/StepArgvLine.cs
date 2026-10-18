namespace Aw.Sindri.Features.ApproveProfile;

/// <summary>One <c lang="text">agents.&lt;id&gt;.stepArgvHash</c> the approval adds or changes, with the entry on this box whose argvHash matches it (launcher L-16): the human approves argv they can read, not an opaque hash.</summary>
/// <param name="Agent">The agent id the hash is listed under.</param>
/// <param name="Hash">The listed hash.</param>
/// <param name="Entry">The matching entry, or null when no entry on this box has this hash.</param>
public sealed record StepArgvLine(string Agent, string Hash, StepArgvEntry? Entry);
