using System.Diagnostics;
using System.Globalization;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>Runs one gh command (read-only) and captures its output. A missing binary, a timeout or a non-zero exit is
/// a <see cref="GhReply"/> with the reason, never an exception. The binary is always started by an absolute path
/// found on PATH (never the current directory or a relative PATH entry), and a gh that outlives its timeout is killed
/// with its process tree.</summary>
/// <param name="settings">Which gh to run and how long to wait.</param>
internal sealed class GhCli(ReadinessSettings settings)
{
    internal async Task<GhReply> Run(string root, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var binary = Resolve(settings.GhBinary, settings.GhSearchPath);
        if (binary is null)
        {
            return GhReply.Failed($"gh could not run ({settings.GhBinary} isn't an absolute path or a gh on an absolute PATH entry)");
        }

        using var process = Start(binary, root, args, out var failure);
        if (process is null)
        {
            return GhReply.Failed(failure);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.GhTimeout);
        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return new GhReply(process.ExitCode, await stdout.ConfigureAwait(false), (await stderr.ConfigureAwait(false)).Trim());
        }
        catch (OperationCanceledException)
        {
            Stop(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return GhReply.Failed(string.Create(CultureInfo.InvariantCulture,
                $"gh {string.Join(' ', args)} timed out after {settings.GhTimeout.TotalSeconds:0.#} s (stopped)"));
        }
    }

    /// <summary>The executable to start: an absolute override as is (if it exists), or the first 'gh' file on an
    /// absolute PATH entry. Relative and empty PATH entries are skipped (they mean the current directory, which may be
    /// the checkout being checked); null when nothing qualifies.</summary>
    internal static string? Resolve(string binary, string? path)
    {
        if (Path.IsPathFullyQualified(binary))
        {
            return File.Exists(binary) ? binary : null;
        }

        if (binary.Contains('/', StringComparison.Ordinal) || binary.Contains('\\', StringComparison.Ordinal))
        {
            return null;
        }

        return (path ?? "").Split(Path.PathSeparator)
            .Where(Path.IsPathFullyQualified)
            .Select(dir => Path.Combine(dir, binary))
            .FirstOrDefault(File.Exists);
    }

    private static Process? Start(string binary, string root, IReadOnlyList<string> args, out string failure)
    {
        var start = new ProcessStartInfo(binary)
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        start.Environment["GH_PROMPT_DISABLED"] = "1";
        start.Environment["NO_COLOR"] = "1";
        try
        {
            failure = "";
            return Process.Start(start) ?? throw new InvalidOperationException("gh did not start");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            failure = $"gh could not run ({binary}: {e.Message})";
            return null;
        }
    }

    private static void Stop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            _ = process.WaitForExit(TimeSpan.FromSeconds(5));
        }
        catch (InvalidOperationException)
        {
            // why: it exited between the timeout and the kill
        }
    }
}
