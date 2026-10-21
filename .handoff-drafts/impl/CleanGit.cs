using System.Diagnostics;
using System.Text;

namespace Aw.CodeIndex.Infrastructure.Snapshot;

/// <summary>
/// How the snapshot starts git (spec §5.1): no inherited GIT_* state, no config but the command line's, no network, no
/// replace refs, no hooks or file-system monitor, and no lock in the owner's checkout. A git that misses its time is killed.
/// </summary>
internal static class CleanGit
{
    private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(5);

    /// <summary>Removes every GIT_* variable (the repo selectors, the object stores, GIT_CONFIG_* and the rest) and sets the hardening ones: no optional locks, no lazy fetch (git 2.44 and later), no system config, no global config.</summary>
    /// <param name="environment">The process environment to change.</param>
    internal static void ScrubEnvironment(IDictionary<string, string?> environment)
    {
        foreach (var name in environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = environment.Remove(name);
        }

        environment["GIT_OPTIONAL_LOCKS"] = "0";
        environment["GIT_NO_LAZY_FETCH"] = "1";
        environment["GIT_CONFIG_NOSYSTEM"] = "1";
        environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
    }

    /// <summary>The arguments that come before the git verb. 'protocol.allow=never' is the guard against a lazy fetch on a git older than 2.44; 'safe.directory' is never set, so a repo git refuses as unsafe stays refused.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <returns>The global arguments.</returns>
    internal static IReadOnlyList<string> GlobalArguments(string repo)
        =>
        [
            "-C", repo, "--no-replace-objects",
            "-c", "protocol.allow=never", "-c", "core.fsmonitor=false", "-c", "core.hooksPath=/dev/null", "-c", "core.quotepath=off",
        ];

    /// <summary>Prepares a git process; the caller starts it.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="redirectInput">Whether the caller writes to git's standard input.</param>
    /// <param name="args">The verb and its arguments.</param>
    /// <returns>The start info.</returns>
    internal static ProcessStartInfo Prepare(string repo, bool redirectInput, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = redirectInput,
            UseShellExecute = false,
            StandardOutputEncoding = null,   // bytes: paths and blobs are read as they are, never decoded by the runtime
        };
        ScrubEnvironment(start.Environment);
        foreach (var arg in (string[])[.. GlobalArguments(repo), .. args])
        {
            start.ArgumentList.Add(arg);
        }

        return start;
    }

    /// <summary>Runs git to its end and keeps its standard output; standard error is drained and dropped, since it may name repo content.</summary>
    /// <param name="start">The prepared process.</param>
    /// <param name="input">Bytes to write to standard input (the start info redirects it), or null.</param>
    /// <param name="cancellationToken">Cancels the call; git is killed.</param>
    /// <returns>The exit code and the output, or null when git did not run or did not finish in time.</returns>
    internal static async Task<(int Exit, byte[] Stdout)?> Run(ProcessStartInfo start, byte[]? input, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_timeout);
        Process? git = null;
        try
        {
            git = Process.Start(start) ?? throw new InvalidOperationException("git didn't start");
            var stderr = git.StandardError.BaseStream.CopyToAsync(Stream.Null, limit.Token);
            var writing = input is null ? Task.CompletedTask : WriteAndClose(git, input, limit.Token);
            using var stdout = new MemoryStream();
            await git.StandardOutput.BaseStream.CopyToAsync(stdout, limit.Token).ConfigureAwait(false);
            await writing.ConfigureAwait(false);
            await git.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            return (git.ExitCode, stdout.ToArray());
        }
        catch (OperationCanceledException)
        {
            Kill(git);   // a git call never outlives the build or its own time
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return null;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            Kill(git);
            return null;
        }
        finally
        {
            git?.Dispose();
        }
    }

    /// <summary>Kills a process that is still running; one that already exited is left alone.</summary>
    /// <param name="git">The process, or null.</param>
    internal static void Kill(Process? git)
    {
        try
        {
            if (git is { HasExited: false })
            {
                git.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // it exited between the check and the kill
        }
    }

    internal static byte[] Lines(IEnumerable<string> lines) => Encoding.ASCII.GetBytes(string.Concat(lines.Select(l => l + "\n")));

    private static async Task WriteAndClose(Process git, byte[] input, CancellationToken cancellationToken)
    {
        try
        {
            await git.StandardInput.BaseStream.WriteAsync(input, cancellationToken).ConfigureAwait(false);
            await git.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // git exited before it read everything: its exit code says why
        }
        finally
        {
            git.StandardInput.Close();
        }
    }
}
