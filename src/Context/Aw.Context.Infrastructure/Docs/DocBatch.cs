namespace Aw.Context.Infrastructure.Docs;

/// <summary>One repo's refresh, ready to write.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Head">The commit the index will reflect.</param>
/// <param name="Ruleset">The scrub ruleset the prepared documents were cleaned with.</param>
/// <param name="Removed">Documents to delete (gone, or about to be rewritten at a new blob).</param>
/// <param name="Prepared">Documents to write.</param>
/// <param name="At">When the refresh ran.</param>
internal sealed record DocBatch(string Repo, string Head, string Ruleset, IReadOnlyList<string> Removed, IReadOnlyList<PreparedDoc> Prepared, DateTimeOffset At);
