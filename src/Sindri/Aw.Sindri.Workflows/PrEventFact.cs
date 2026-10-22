namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The 'fact:prEvent' of a 'pr.changed' event (PR babysitter spec §6.1).</summary>
/// <param name="Subject">The pull request's link exactly as the instance's 'fact:pr' holds it.</param>
/// <param name="HeadSha">The head the poll saw.</param>
/// <param name="Changes">Every change, in the outcome order; empty for 'idle'.</param>
/// <param name="At">When the poll saw it.</param>
internal sealed record PrEventFact(string Subject, string HeadSha, IReadOnlyList<string> Changes, DateTimeOffset At);
