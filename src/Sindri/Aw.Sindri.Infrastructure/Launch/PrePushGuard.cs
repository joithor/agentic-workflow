using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The pre-push hook that makes a launched agent Step unable to push (line spec LN-LCH1). It sits in the hooks directory git resolves for the Step's
/// working directory (a linked worktree shares its repo's), so it refuses only where <see cref="LaunchEnvironment.Worktree"/> is set: the owner's own pushes pass.
/// </summary>
internal static class PrePushGuard
{
    private const string Marker = "# aw-sindri pre-push guard (LN-LCH1)";

    private static readonly string _script =
        "#!/bin/sh\n"
        + Marker + "\n"
        + $"if [ -n \"${{{LaunchEnvironment.Worktree}:-}}\" ]; then\n"
        + $"  echo \"{SindriErrors.LaunchPushRefused.Code}: agent Steps can't push; Sindri pushes\" >&2\n"
        + "  exit 1\n"
        + "fi\n"
        + "exit 0\n";

    /// <summary>Installs the hook when none is there or the one there is ours; a directory that isn't a git checkout is left alone.</summary>
    /// <param name="workDir">The Step's working directory.</param>
    /// <param name="cancellationToken">Cancels the git query.</param>
    /// <returns>Null, or SND-LAUNCH-022 when a hook that isn't ours is in the way (it is never changed).</returns>
    internal static async ValueTask<Error?> Install(string workDir, CancellationToken cancellationToken)
    {
        if (await ViewerProcess.Output("git", ["-C", workDir, "rev-parse", "--git-path", "hooks"], cancellationToken).ConfigureAwait(false) is not { Length: > 0 } hooks)
        {
            return null;
        }

        var hooksDirectory = Path.GetFullPath(hooks, workDir);
        var hook = Path.Combine(hooksDirectory, "pre-push");
        try
        {
            if (File.Exists(hook) && !(await File.ReadAllTextAsync(hook, cancellationToken).ConfigureAwait(false)).Contains(Marker, StringComparison.Ordinal))
            {
                return SindriErrors.LaunchPrePushForeign.With($"{hook} is not Sindri's (fix: move it aside or fold its checks into the repo's own tooling; a Step is never launched over it)");
            }

            _ = Directory.CreateDirectory(hooksDirectory);
            await File.WriteAllTextAsync(hook, _script, cancellationToken).ConfigureAwait(false);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.LaunchPrePushForeign.With($"the pre-push guard could not be written at {hook}: {e.Message}");
        }
    }
}
