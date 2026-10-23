using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Infrastructure.Store;

/// <summary>Where a build file lives and what the build holds while it runs.</summary>
/// <param name="Files">The index directory.</param>
/// <param name="Repo">The repo.</param>
/// <param name="Ulid">The build file's ULID.</param>
/// <param name="Hold">The repo's index lock, released when the build is disposed (published or not).</param>
internal sealed record BuildTarget(IndexFiles Files, RepoName Repo, string Ulid, IAsyncDisposable Hold);
