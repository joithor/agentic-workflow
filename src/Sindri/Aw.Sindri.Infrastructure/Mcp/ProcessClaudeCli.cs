using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Infrastructure.Mcp;

/// <summary>Runs the claude CLI as a child process with an argument list (no shell), bounded by the settings' budget (30 s).</summary>
/// <param name="settings">Which executable.</param>
internal sealed class ProcessClaudeCli(McpSettings settings) : IClaudeCli
{
    public async ValueTask<ClaudeRun?> Run(IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(workingDirectory))
        {
            return null;   // the profile's checkout was moved or deleted: claude can't be started there
        }

        if (Executable(settings.ClaudeBin, settings.SearchPath) is not { } executable)
        {
            return null;   // not on PATH, or not executable
        }

        var start = new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process? claude;
        try
        {
            claude = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return null;   // not installed, or not executable
        }

        if (claude is null)
        {
            return null;
        }

        using (claude)
        {
            return await Finish(claude, settings.ClaudeBudget, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<ClaudeRun> Finish(Process claude, TimeSpan budget, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(budget);
        var output = claude.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = claude.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await claude.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Stop(claude);   // over budget or cancelled: never leave the child running
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new ClaudeRun(124, string.Create(CultureInfo.InvariantCulture, $"claude didn't finish within {budget.TotalSeconds:0} s"));
        }

        _ = await output.ConfigureAwait(false);
        var lines = (await error.ConfigureAwait(false)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new ClaudeRun(claude.ExitCode, lines.Length == 0 ? string.Empty : lines[^1]);
    }

    private static void Stop(Process claude)
    {
        try
        {
            claude.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // it exited between the timeout and the kill
        }
    }

    /// <summary>
    /// A name with a '/' as given; a bare name looked up in the settings' search path only (PATH's absolute entries), and
    /// only an executable file counts. Process.Start would also try the working directory (the work repo) first, and a
    /// file named 'claude' there must never run.
    /// </summary>
    private static string? Executable(string name, IReadOnlyList<string> searchPath) =>
        name.Contains('/', StringComparison.Ordinal)
            ? (IsExecutable(name) ? name : null)
            : searchPath.Select(directory => Path.Combine(directory, name)).FirstOrDefault(IsExecutable);

    private static bool IsExecutable(string path) =>
        File.Exists(path) && (OperatingSystem.IsWindows() || (File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != UnixFileMode.None);

}
