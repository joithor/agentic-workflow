using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Kernel;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Creates a Step's worktree when it is missing.</summary>
internal static class LaunchWorktree
{
    internal static async ValueTask<Error?> Ensure(LaunchRequest request, CancellationToken cancellationToken) =>
        request.Worktree is { } worktree && !Directory.Exists(worktree)
        && await ViewerProcess.Output("git", ["-C", request.RepoPath, "worktree", "add", "--detach", worktree], cancellationToken).ConfigureAwait(false) is null
            ? SindriErrors.LaunchRepoMissing.With($"the Step's worktree could not be created at {worktree}")
            : null;
}
