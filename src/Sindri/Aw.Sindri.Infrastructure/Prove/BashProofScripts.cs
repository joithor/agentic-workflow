using System.ComponentModel;
using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Infrastructure.Prove;

/// <summary>The recorded evidence scripts through bash (PB spec §8): 'bash &lt;script&gt;' with the worktree as its working directory, an argument list never a shell string, bounded to 30 minutes and killed with its caller.</summary>
internal sealed class BashProofScripts : IProofScripts
{
    private const int TailLimit = 2000;

    private static readonly TimeSpan _budget = TimeSpan.FromMinutes(30);

    public async ValueTask<Result<ProofScriptRun>> Run(string worktree, string script, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worktree);
        ArgumentNullException.ThrowIfNull(script);
        var start = new ProcessStartInfo("bash", [script]) { WorkingDirectory = worktree, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Exception e) when (e is Win32Exception or DirectoryNotFoundException)
        {
            return WorkflowErrors.EffectFailed.With($"bash can't run {script} in {worktree}: {e.Message}");
        }

        using (process)
        {
            return process is null ? WorkflowErrors.EffectFailed.With($"bash didn't start for {script}") : await Answer(process, script, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<Result<ProofScriptRun>> Answer(Process process, string script, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_budget);
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            var error = process.StandardError.ReadToEndAsync(deadline.Token);
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            var text = await error.ConfigureAwait(false) + await output.ConfigureAwait(false);
            return new ProofScriptRun(process.ExitCode == 0, $"exit {process.ExitCode}: {Tail(text)}");
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);   // the budget or the caller's cancellation: never leave the script running
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return new ProofScriptRun(false, $"{script} didn't finish within {_budget.TotalMinutes:0} minutes");
        }
    }

    private static string Tail(string text) => text.Length <= TailLimit ? text.Trim() : text[^TailLimit..].Trim();
}
