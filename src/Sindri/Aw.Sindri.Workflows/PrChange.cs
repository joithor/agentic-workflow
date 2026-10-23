namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>One 'pr.changed' to record.</summary>
/// <param name="Subject">The pull request and its items.</param>
/// <param name="Outcome">The first change in the outcome order, or 'idle'.</param>
/// <param name="Changes">Every change, in the outcome order; empty for 'idle'.</param>
/// <param name="HeadSha">The head the poll saw.</param>
/// <param name="What">What happened, in words that stay the same when the same change is found again: the idempotency key is made of it.</param>
internal sealed record PrChange(PrSubject Subject, string Outcome, IReadOnlyList<string> Changes, string HeadSha, string What);
