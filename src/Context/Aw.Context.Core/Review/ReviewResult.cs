using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Review;

/// <summary>What a review call wrote.</summary>
/// <param name="Written">The override rows it added, in order (accept-deterministic: one per entry; a file: one per matching entry).</param>
/// <param name="Skipped">Entries of a review file that matched nothing in the latest draft (listed, not an error unless none matched: SND-CONTEXT-043).</param>
/// <param name="Undecided">How many entries are still undecided afterwards.</param>
public sealed record ReviewResult(IReadOnlyList<OverrideId> Written, IReadOnlyList<string> Skipped, int Undecided);
