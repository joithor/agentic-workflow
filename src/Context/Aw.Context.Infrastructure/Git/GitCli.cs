using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

/// <summary>
/// Read-only git calls for the repo-docs source (spec §5.1 argument rules): shas match ^[0-9a-f]{7,64}$ and never start
/// with '-', every path comes after '--', the arguments go through ArgumentList (no shell), and git runs with the
/// caller's GIT_* variables removed. A git failure is SND-CONTEXT-010 (the source is unavailable); a missing object
/// is NotFound.
/// </summary>
internal sealed partial class GitCli
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

    /// <summary>The commit HEAD points at.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The sha.</returns>
    public async ValueTask<Result<string>> Head(string repo, CancellationToken cancellationToken)
    {
        var head = await Run(repo, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], cancellationToken).ConfigureAwait(false);
        return head.Bind<string>(h => IsSha(h.Trim()) ? h.Trim() : ContextErrors.SourceUnavailable.With("git rev-parse HEAD didn't answer a sha"));
    }

    /// <summary>Every blob at <paramref name="commit"/>: path to blob sha.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">A commit sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tree, '/'-separated paths.</returns>
    public async ValueTask<Result<IReadOnlyDictionary<string, string>>> Tree(string repo, string commit, CancellationToken cancellationToken)
    {
        if (!IsSha(commit))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var listed = await Run(repo, ["ls-tree", "-r", "-z", "--full-tree", commit], cancellationToken).ConfigureAwait(false);
        return listed.Map<IReadOnlyDictionary<string, string>>(text => text.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split('\t', 2))
            .Where(parts => parts.Length == 2 && parts[0].Split(' ') is [_, "blob", _])
            .ToDictionary(parts => parts[1], parts => parts[0].Split(' ')[2], StringComparer.Ordinal));
    }

    /// <summary>A blob's text.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="blob">The blob sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The text, or NotFound (SND-CONTEXT-003) when the repo has no such blob.</returns>
    public async ValueTask<Result<string>> Blob(string repo, string blob, CancellationToken cancellationToken)
    {
        if (!IsSha(blob))
        {
            return ContextErrors.UnknownAnchor.With("not a blob sha");
        }

        var exists = await Run(repo, ["cat-file", "-e", blob + "^{blob}"], cancellationToken).ConfigureAwait(false);
        return exists.IsError
            ? ContextErrors.UnknownAnchor.With("the repo has no such blob")
            : await Run(repo, ["cat-file", "blob", blob], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>True for a full or abbreviated lower-case hex sha (never an option, never a ref expression).</summary>
    /// <param name="text">The candidate.</param>
    /// <returns>Whether it is a sha.</returns>
    internal static bool IsSha(string text) => Sha.IsMatch(text);

    private async ValueTask<Result<string>> Run(string repo, string[] args, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = start.Environment.Remove(name);
        }

        foreach (var arg in (string[])["-C", repo, "-c", "core.quotepath=off", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        return await Execute(start, args[0], cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<string>> Execute(ProcessStartInfo start, string verb, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            using var git = Process.Start(start) ?? throw new InvalidOperationException("git didn't start");
            var stdout = git.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = git.StandardError.ReadToEndAsync(timeout.Token);
            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            _ = await stderr.ConfigureAwait(false);   // drained, never logged: it may name repo content
            return git.ExitCode == 0
                ? await stdout.ConfigureAwait(false)
                : ContextErrors.SourceUnavailable.With($"git {verb} failed (exit {git.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception
            || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return ContextErrors.SourceUnavailable.With($"git {verb} didn't run: {e.GetType().Name}");
        }
    }

    [GeneratedRegex("^[0-9a-f]{7,64}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha { get; }
}
