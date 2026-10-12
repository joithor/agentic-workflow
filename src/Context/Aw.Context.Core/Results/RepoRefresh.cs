using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Results;

/// <summary>One repo's refresh: counts and codes only, never paths' contents.</summary>
/// <param name="Repo">The repo.</param>
/// <param name="Head">The commit it now reflects, or null when it couldn't be read.</param>
/// <param name="Added">Documents indexed anew.</param>
/// <param name="Removed">Documents dropped (gone from HEAD or from the globs).</param>
/// <param name="Kept">Documents unchanged.</param>
/// <param name="Blocked">Documents the scrubber withheld (indexed without text).</param>
/// <param name="Code">The error code when the repo couldn't be refreshed, else null.</param>
public sealed record RepoRefresh(string Repo, SourceVersion? Head, int Added, int Removed, int Kept, int Blocked, string? Code);
