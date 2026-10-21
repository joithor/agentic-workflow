using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>One repo's index, from its files only (spec §7 Status).</summary>
/// <param name="Repo">The repo.</param>
/// <param name="State">Missing, ok or damaged.</param>
/// <param name="Commit">The commit the index describes, or null when missing or damaged.</param>
/// <param name="BuiltAt">When it was built, or null.</param>
/// <param name="Layers">Each layer's status; empty when missing or damaged.</param>
/// <param name="Counts">Files, symbols and edges from the header, or null.</param>
public sealed record RepoStatus(RepoName Repo, IndexState State, CommitSha? Commit, DateTimeOffset? BuiltAt, IReadOnlyList<LayerStatus> Layers, IndexCounts? Counts);
