using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>Builds or updates one repo's index at a commit (spec §5.7).</summary>
/// <param name="Repo">The repo and its main checkout, with its effective index settings.</param>
/// <param name="Commit">The commit to index; it must still be on the default branch.</param>
/// <param name="Progress">Told a counts-only line per step ('structure 1,400 of 3,100 files').</param>
public sealed record BuildCodeIndexRequest(CodeIndexRepo Repo, CommitSha Commit, IProgress<string> Progress);
