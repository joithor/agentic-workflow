using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Lenses;

/// <summary>What 'context filemap' asks for.</summary>
/// <param name="Repo">A repo of the profile.</param>
/// <param name="Path">A directory in the repo ('' for the root).</param>
/// <param name="UnmappedOnly">True to keep only unmapped and suggested children.</param>
/// <param name="Draft">True to read the latest draft instead of the current map.</param>
public sealed record FileMapQuery(ContextRepoName Repo, string Path, bool UnmappedOnly, bool Draft);
