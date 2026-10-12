using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Engine.Desk;

/// <summary>
/// Completes a desk Step (port; engine spec rev 8 §5.3): 'sindri workflow complete --run &lt;token&gt;' reports the
/// outcome and output files of the open desk run, and the instance moves on. Engine P4a: the adapter completes the
/// RunStep activity through its task token; P4 routes it through the API's CompleteStep slice.
/// </summary>
public interface IDeskSteps
{
    /// <summary>Completes the open desk run <see cref="DeskReport.Run"/> of <see cref="DeskReport.Address"/>.</summary>
    /// <param name="report">The address, run token, outcome and output files.</param>
    /// <param name="cancellationToken">Cancels the completion and the wait for the next state.</param>
    /// <returns>The instance's state after the report, or SND-WF-005 (no open desk run with that token) or SND-API-001 (Temporal unreachable).</returns>
    public ValueTask<Result<DeskOutcome>> Complete(DeskReport report, CancellationToken cancellationToken);
}
