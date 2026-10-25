using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Model;

/// <summary>The kept files of a commit and a count per skip reason (spec §5.1). Names and paths of skipped entries are never kept.</summary>
/// <param name="Commit">The commit it describes.</param>
/// <param name="Files">The kept regular files, in path order.</param>
/// <param name="Skipped">How many entries were skipped, per reason; a reason with none is absent.</param>
public sealed record SnapshotInventory(CommitSha Commit, IReadOnlyList<SnapshotFile> Files, IReadOnlyDictionary<SkipReason, int> Skipped);
