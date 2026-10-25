using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>What every answer carries (spec §7.1): which index it came from and the layer it depends on. Sindri compares <paramref name="Commit"/> with the checkout's head to say 'stale'.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Commit">The commit the index describes.</param>
/// <param name="BuiltAt">When it was built.</param>
/// <param name="State">Ok (a readable index; missing and damaged are errors).</param>
/// <param name="Layer">The layer the answer depends on and its state: 'graph: unavailable' says the edges are structure-only.</param>
public sealed record AnswerStamp(RepoName Repo, CommitSha Commit, DateTimeOffset BuiltAt, IndexState State, LayerStatus Layer);
