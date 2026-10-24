using Aw.Kernel;
using Aw.Sindri.Core.Checks;

namespace Aw.Sindri.Core.Setup;

/// <summary>The git reads of the pre-commit hook (cutover spec §6.2): where a repo's hook lives and what its staged diff adds. The adapter is the only place that starts git.</summary>
public interface IPreCommitGit
{
    /// <summary>Resolves the hook path with 'git rev-parse --git-path hooks/pre-commit'.</summary>
    /// <param name="repo">The repo's folder; null is the current directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The location, or SND-SCRUB-004 when the folder is not a git repo.</returns>
    public ValueTask<Result<HookLocation>> HookPath(string? repo, CancellationToken cancellationToken);

    /// <summary>Reads the lines the staged diff adds ('git diff --cached --unified=0').</summary>
    /// <param name="repo">The repo's folder; null is the current directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The added lines, SND-SCRUB-004 outside a repo, or SND-SCRUB-005 when the diff failed.</returns>
    public ValueTask<Result<IReadOnlyList<AddedLine>>> StagedAddedLines(string? repo, CancellationToken cancellationToken);
}
