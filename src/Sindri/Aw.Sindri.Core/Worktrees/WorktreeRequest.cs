using Aw.Kernel;

namespace Aw.Sindri.Core.Worktrees;

/// <summary>What <see cref="IWorktrees.Create"/> is asked for.</summary>
/// <param name="RepoRoot">The repo's main checkout, an absolute path.</param>
/// <param name="BaseCommit">The full 40-hex commit sha the branch starts at; never a ref.</param>
/// <param name="Branch">The branch the worktree checks out (created at the base when it does not exist).</param>
/// <param name="Path">Where the worktree goes, an absolute path.</param>
public sealed record WorktreeRequest(string RepoRoot, string BaseCommit, string Branch, string Path)
{
    /// <summary>Checks the request without touching the disk.</summary>
    /// <returns>SND-WT-001 naming the first problem, or null when the request is well formed.</returns>
    public Error? Validate() => this switch
    {
        _ when !IsFullSha(BaseCommit) => Invalid("BaseCommit must be a full 40-character lower-case hex sha, never a ref"),
        _ when !IsAbsolute(RepoRoot) => Invalid("RepoRoot must be an absolute path"),
        _ when !IsAbsolute(Path) => Invalid("Path must be an absolute path"),
        _ when string.IsNullOrWhiteSpace(Branch) || Branch.StartsWith('-') || Branch.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) => Invalid("Branch must be a name without spaces that does not start with '-'"),
        _ => null,
    };

    private static bool IsFullSha(string value) => value.Length == 40 && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static bool IsAbsolute(string value) => value.StartsWith('/') || (value.Length >= 3 && char.IsAsciiLetter(value[0]) && value[1] == ':' && value[2] is '\\' or '/');

    private static Error Invalid(string reason) => SindriErrors.WorktreeInvalid.With(reason);
}
