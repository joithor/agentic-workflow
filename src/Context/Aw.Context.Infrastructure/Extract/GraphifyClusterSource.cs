using System.Diagnostics;
using System.Formats.Tar;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Git;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>
/// Clusters a repo's code with graphify, pinned and sandboxed (network denied, writes confined to a private snapshot of
/// the commit). Absent, wrong version, no sandbox or a failed run is an unavailable, empty set, never an error: the build
/// goes on with deterministic anchors and says 'degraded: clusters'. graphify never runs unsandboxed.
/// </summary>
/// <param name="git">Exports the commit.</param>
/// <param name="options">The pin and limits.</param>
internal sealed partial class GraphifyClusterSource(GitCli git, GraphifyOptions options) : IClusterSource
{
    /// <summary>graphify's private temp and cache folder inside the snapshot (the only writable path); a repo's tracked files never land under it.</summary>
    internal const string PrivateFolder = ".sindri-tmp";

    private const long MaxFileBytes = 2L * 1024 * 1024;
    private const long MaxTotalBytes = 256L * 1024 * 1024;
    private static readonly ClusterSet _unavailable = new([], Available: false);

    public async ValueTask<Result<ClusterSet>> Clusters(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var host = SandboxHost.Current();
        if (Find(options.Binary) is not { } binary || !await Usable(host, binary, cancellationToken).ConfigureAwait(false))
        {
            return _unavailable;
        }

        var snapshot = Directory.CreateTempSubdirectory("aw-graphify-").FullName;
        try
        {
            var exported = await git.Archive(repo.Path, commit, (stream, token) => Unpack(stream, snapshot, repo, token), cancellationToken).ConfigureAwait(false);
            return exported.IsError ? _unavailable : await Run(host, binary, snapshot, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DeleteQuietly(snapshot);
        }
    }

    private async ValueTask<Result<ClusterSet>> Run(SandboxHost host, string binary, string snapshot, CancellationToken cancellationToken)
    {
        var real = host.RealPath(snapshot);
        var priv = Directory.CreateTempSubdirectory(Path.Combine(real, PrivateFolder + "-")).FullName;
        _ = Directory.CreateDirectory(Path.Combine(priv, "cache"));
        var argv = GraphifySandbox.Argv(host, [binary, "extract", real, "--code-only", "--no-viz"], [real]);
        if (argv is null)
        {
            return _unavailable;
        }

        var env = PrivateEnvironment(priv);
        var ran = await Spawn(argv, env, real, options.Timeout, cancellationToken).ConfigureAwait(false);
        if (ran is not { Exit: 0 } || ReadGraph(real) is not { } json)
        {
            return _unavailable;
        }

        var clusters = GraphifyGraph.Clusters(json, [snapshot, real]);
        return clusters.IsError ? _unavailable : new ClusterSet(clusters.Value, Available: true);
    }

    // Absent, the wrong version, or a sandbox that can't start all read the same: --version run inside the sandbox must print the pin.
    private async ValueTask<bool> Usable(SandboxHost host, string binary, CancellationToken cancellationToken)
    {
        var scratch = Directory.CreateTempSubdirectory("aw-graphify-probe-").FullName;
        try
        {
            var argv = GraphifySandbox.Argv(host, [binary, "--version"], []);
            var ran = argv is null ? null : await Spawn(argv, PrivateEnvironment(scratch), scratch, TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            return ran is { Exit: 0 } && Version.Match(ran.Stdout) is { Success: true } found && string.Equals(found.Value, options.Pin, StringComparison.Ordinal);
        }
        finally
        {
            DeleteQuietly(scratch);
        }
    }

    private static Dictionary<string, string> PrivateEnvironment(string priv) => new(StringComparer.Ordinal)
    {
        ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? "/usr/bin:/bin",
        ["LANG"] = "C.UTF-8",
        ["HOME"] = priv,
        ["TMPDIR"] = priv,
        ["XDG_CACHE_HOME"] = Path.Combine(priv, "cache"),
        ["UV_CACHE_DIR"] = Path.Combine(priv, "cache", "uv"),
        ["PYTHONPYCACHEPREFIX"] = Path.Combine(priv, "pycache"),
    };

    // The binary's absolute path: as given, or the first match on PATH.
    private static string? Find(string binary)
    {
        if (Path.IsPathRooted(binary))
        {
            return File.Exists(binary) ? binary : null;
        }

        return (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir, binary)).FirstOrDefault(File.Exists);
    }

    // graph.json is the sandboxed tool's output and is read unsandboxed: never through a link, only a regular file, never over the cap.
    private string? ReadGraph(string snapshot)
    {
        try
        {
            var folder = new DirectoryInfo(Path.Combine(snapshot, "graphify-out"));
            var file = new FileInfo(Path.Combine(folder.FullName, "graph.json"));
            return folder.Exists && folder.LinkTarget is null && file.Exists && file.LinkTarget is null && file.Length <= options.MaxGraphBytes
                ? File.ReadAllText(file.FullName, Encoding.UTF8)
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Writes the tar's regular files under the snapshot: no link, no path that climbs out, nothing in graphify's own folders, no oversized file, and no path the repo denies (secrets never reach graphify).
    internal static async Task Unpack(Stream tar, string snapshot, ContextRepo repo, CancellationToken cancellationToken)
    {
        long total = 0;
        var reader = new TarReader(tar, leaveOpen: true);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false) is { } entry)
            {
                if (SafePath(entry) is not { } relative || repo.Denies(relative) || entry.DataStream is null || entry.Length > MaxFileBytes || (total += entry.Length) > MaxTotalBytes)
                {
                    continue;
                }

                var target = Path.Combine(snapshot, relative);
                _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);   // why: target is a file path under the snapshot
                var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await using (file.ConfigureAwait(false))
                {
                    await entry.DataStream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    // The entry's path when it is a regular file that stays inside the snapshot and outside graphify's own folders; else null.
    private static string? SafePath(TarEntry entry)
    {
        var segments = entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return entry.EntryType != TarEntryType.RegularFile || segments.Length == 0 || segments.Contains("..") || segments.Contains(".")
            || segments[0].StartsWith("graphify-out", StringComparison.Ordinal) || segments[0].StartsWith(PrivateFolder, StringComparison.Ordinal)
            ? null
            : string.Join('/', segments);
    }

    // Runs argv with exactly this environment; null when it can't start or doesn't finish in time. Output is drained and capped, never logged.
    private static async Task<Ran?> Spawn(IReadOnlyList<string> argv, Dictionary<string, string> environment, string directory, TimeSpan limit, CancellationToken cancellationToken)
    {
        var start = StartInfo(argv, environment, directory);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limit);
        Process? process = null;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException("the process didn't start");
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            _ = await stderr.ConfigureAwait(false);
            return new Ran(process.ExitCode, await stdout.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return null;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static ProcessStartInfo StartInfo(IReadOnlyList<string> argv, Dictionary<string, string> environment, string directory)
    {
        var start = new ProcessStartInfo(argv[0]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = directory };
        start.Environment.Clear();
        foreach (var (name, value) in environment)
        {
            start.Environment[name] = value;
        }

        foreach (var arg in argv.Skip(1))
        {
            start.ArgumentList.Add(arg);
        }

        return start;
    }

    private static void Kill(Process? process)
    {
        try
        {
            if (process is { HasExited: false })
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // it exited between the check and the kill
        }
    }

    private static void DeleteQuietly(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a temp folder that stays is harmless; the next run makes its own
        }
    }

    [GeneratedRegex("[0-9]+\\.[0-9]+\\.[0-9]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Version { get; }

    private sealed record Ran(int Exit, string Stdout);
}
