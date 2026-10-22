using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Forge;

/// <summary>
/// <see cref="IForgeRepos"/> from the workspace's 'origin' remote ('git -C workspace remote get-url origin'), as an https, ssh:// or scp-style url
/// ('https://github.com/owner/name.git', 'git@github.com:owner/name.git').
/// </summary>
/// <param name="runner">Runs git.</param>
internal sealed class GitForgeRepos(IToolRunner runner) : IForgeRepos
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    public ValueTask<Result<PullRepo>> OfWorkspace(string workspace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var run = runner.Run("git", ["-C", workspace, "remote", "get-url", "origin"], _timeout, options: null, cancellationToken);
        Result<PullRepo> result = run.Outcome == ToolOutcome.Exited && run.ExitCode == 0 && Parse(run.Output.Trim()) is { } repo
            ? repo
            : SindriErrors.NoForge.With($"{workspace} has no 'origin' remote that names a forge repository (fix: git remote add origin <url>)");
        return ValueTask.FromResult(result);
    }

    /// <summary>The repository a remote url names; null for a url that isn't host/owner/name.</summary>
    /// <param name="url">The remote url.</param>
    /// <returns>The repository, or null.</returns>
    internal static PullRepo? Parse(string url)
    {
        string host;
        string path;
        if (url.Contains("://", StringComparison.Ordinal) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            (host, path) = (uri.Host, uri.AbsolutePath.Trim('/'));
        }
        else if (url.IndexOf(':', StringComparison.Ordinal) is var colon and > 0 && !url.Contains("://", StringComparison.Ordinal))
        {
            (host, path) = (url[..colon][(url.IndexOf('@', StringComparison.Ordinal) + 1)..], url[(colon + 1)..].Trim('/'));
        }
        else
        {
            return null;
        }

        var parts = (path.EndsWith(".git", StringComparison.Ordinal) ? path[..^4] : path).Split('/');
        return host.Length > 0 && parts is [{ Length: > 0 } owner, { Length: > 0 } name] ? new PullRepo(host, owner, name) : null;
    }
}
