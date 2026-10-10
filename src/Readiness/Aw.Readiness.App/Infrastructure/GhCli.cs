using System.Diagnostics;
using System.Globalization;

namespace Aw.Readiness.App.Infrastructure;

/// <summary>Runs one gh command (read-only) and captures its output. A missing binary, a timeout or a non-zero exit is
/// a <see cref="GhReply"/> with the reason, never an exception.</summary>
/// <param name="settings">Which gh to run and how long to wait.</param>
internal sealed class GhCli(ReadinessSettings settings)
{
    internal async Task<GhReply> Run(string root, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(settings.GhBinary)
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
            using var process = Process.Start(start) ?? throw new InvalidOperationException("gh did not start");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(settings.GhTimeout);
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            return new GhReply(process.ExitCode, await stdout.ConfigureAwait(false), (await stderr.ConfigureAwait(false)).Trim());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return GhReply.Failed(string.Create(CultureInfo.InvariantCulture, $"gh {string.Join(' ', args)} timed out after {settings.GhTimeout.TotalSeconds:0} s"));
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return GhReply.Failed($"gh could not run ({settings.GhBinary}: {e.Message})");
        }
    }
}
