using System.Globalization;
using System.ComponentModel;
using System.Diagnostics;
using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Infrastructure.Mcp;

/// <summary>Runs the claude CLI as a child process with an argument list (no shell), bounded by the settings' budget (30 s).</summary>
/// <param name="settings">Which executable.</param>
internal sealed class ProcessClaudeCli(McpSettings settings) : IClaudeCli
{
    public async ValueTask<ClaudeRun?> Run(IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(settings.ClaudeBin, arguments)
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            claude.Kill(entireProcessTree: true);
            return new ClaudeRun(124, string.Create(CultureInfo.InvariantCulture, $"claude didn't finish within {budget.TotalSeconds:0} s"));
        }

        _ = await output.ConfigureAwait(false);
        var lines = (await error.ConfigureAwait(false)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new ClaudeRun(claude.ExitCode, lines.Length == 0 ? string.Empty : lines[^1]);
    }
}
