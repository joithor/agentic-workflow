using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Control;

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
        var start = new ProcessStartInfo(command, ["--version"])
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return null;   // not installed
        }

        if (process is null)
        {
            return null;
        }

        using (process)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_timeout);
            try
            {
                process.StandardInput.Close();
                var text = await process.StandardOutput.ReadToEndAsync(deadline.Token).ConfigureAwait(false);
                await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
                return process.ExitCode == 0 && Version.Match(text) is { Success: true } match ? match.Value : null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                return null;   // took too long
            }
        }
    }

    [GeneratedRegex(@"\d+(\.\d+)+", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Version { get; }
}
