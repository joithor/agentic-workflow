using System.Diagnostics;

namespace Aw.Context.Infrastructure.Extract;

/// <summary>Runs one command with exactly the environment given, bounded by a time limit, and kills its tree at the limit.</summary>
internal static class GraphifyProcess
{
    /// <summary>How a graphify process ended.</summary>
    /// <param name="Exit">The exit code; -1 when it timed out.</param>
    /// <param name="Stdout">What it printed (empty when it timed out).</param>
    /// <param name="TimedOut">True when it was killed at the time limit.</param>
    internal sealed record Ran(int Exit, string Stdout, bool TimedOut = false);

    /// <summary>
    /// Runs argv with exactly this environment; null when it can't start. A run that doesn't finish within <paramref name="limit"/> (null: no limit) is killed and answered as timed out.
    /// Output is drained, never logged.
    /// </summary>
    internal static async Task<Ran?> Spawn(IReadOnlyList<string> argv, Dictionary<string, string> environment, string directory, TimeSpan? limit, CancellationToken cancellationToken)
    {
        var start = StartInfo(argv, environment, directory);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limit ?? Timeout.InfiniteTimeSpan);
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

            return new Ran(-1, string.Empty, TimedOut: true);
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
}
