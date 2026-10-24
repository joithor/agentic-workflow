using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.Scrub;

/// <summary>Writes the v3 pre-commit hook where git says, replacing a TS hook and refusing anyone else's (SND-SCRUB-003). Shared by 'scrub --install-pre-commit' and the onboarding hook slot.</summary>
/// <param name="git">Resolves the hook path.</param>
internal sealed class PreCommitInstaller(IPreCommitGit git) : IPreCommitInstaller
{
    /// <inheritdoc/>
    public async ValueTask<Result<string>> Install(string? repo, string bin, CancellationToken cancellationToken)
    {
        var where = await git.HookPath(repo, cancellationToken).ConfigureAwait(false);
        if (where.IsError)
        {
            return where.Error;
        }

        var path = where.Value.Path;
        if (!where.Value.InGitDir)
        {
            return SindriErrors.ScrubForeignHook.With($"core.hooksPath points at {Path.GetDirectoryName(path)}, which Sindri did not create; add 'sindri scrub --staged' to the hooks there by hand");
        }

        var existing = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : null;
        if (PreCommitHook.Classify(existing) == HookKind.Foreign)
        {
            return SindriErrors.ScrubForeignHook.With($"{path} is not Sindri's; add 'sindri scrub --staged || exit 1' to it by hand");
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);   // why: git-path always names a file inside a hooks directory
        await File.WriteAllTextAsync(path, PreCommitHook.Body(bin), cancellationToken).ConfigureAwait(false);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        return path;
    }

    /// <inheritdoc/>
    public async ValueTask<HookKind> Kind(string repo, CancellationToken cancellationToken)
    {
        var where = await git.HookPath(repo, cancellationToken).ConfigureAwait(false);
        return where.IsError || !File.Exists(where.Value.Path)
            ? HookKind.None
            : PreCommitHook.Classify(await File.ReadAllTextAsync(where.Value.Path, cancellationToken).ConfigureAwait(false));
    }
}
