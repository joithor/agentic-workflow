using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>
/// A harness's installed version from '&lt;command&gt; --version' on PATH: the first version number it prints ('2.1.296
/// (Claude Code)', 'codex-cli 0.41.0'). A command that is missing, fails or takes over five seconds has no version.
/// </summary>
internal sealed partial class CommandHarnessVersions : IHarnessVersions
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public async ValueTask<string?> Read(string command, CancellationToken cancellationToken)
    {
        using var process = Start(command);
        return process is null ? null : await Output(process, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>'&lt;command&gt; --version' started with its streams redirected, or null when it isn't installed.</summary>
    private static Process? Start(string command)
    {
        var start = ChildEnvironment.Start(command, ["--version"]);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.RedirectStandardInput = true;
        try
        {
            return Process.Start(start);
        }
        catch (Win32Exception)
        {
            return null;   // not installed
        }
    }

    /// <summary>The version it prints within the timeout; it is killed if it is still running when the read ends.</summary>
    private static async ValueTask<string?> Output(Process process, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_timeout);
        try
        {
            process.StandardInput.Close();
            var stderr = process.StandardError.ReadToEndAsync(deadline.Token);   // drained, so a chatty harness can't fill the pipe and stall
            var text = await process.StandardOutput.ReadToEndAsync(deadline.Token).ConfigureAwait(false);
            _ = await stderr.ConfigureAwait(false);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            return process.ExitCode == 0 && Version.Match(text) is { Success: true } match ? match.Value : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;   // took too long
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);   // on the timeout or the caller's cancel, never left running
            }
        }
    }

    [GeneratedRegex(@"\d+(\.\d+)+", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Version { get; }
}
