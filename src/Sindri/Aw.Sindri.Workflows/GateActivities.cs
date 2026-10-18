using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'EvaluateGates' (engine P7 plan D7, workflow engine spec §5.2): runs the gates of the transition a trigger matches, one
/// call per gate in the transition's order, over the registered <see cref="Aw.Sindri.Core.Workflow.Blocks.IGate"/>
/// runtimes, and answers what the pure decision reads as its gate answers. The interpreter runs it as a local activity
/// before it decides, so a replay reads the recorded answers and runs no gate again.
/// </summary>
/// <param name="dispatch">Runs a gate by id.</param>
/// <param name="contents">Reads a stored fact a gate's argument names.</param>
/// <param name="logger">The log a gate that threw is noted in.</param>
internal sealed class GateActivities(GateDispatch dispatch, IFactContents contents, ILogger<GateActivities> logger)
{
    /// <summary>The activity: <see cref="Evaluate"/> under the activity's cancellation.</summary>
    /// <param name="run">The gates and what they evaluate.</param>
    /// <returns>The answers, in the transition's order.</returns>
    [Activity("EvaluateGates")]
    public Task<IReadOnlyList<GateAnswer>> EvaluateGatesAsync(GateRun run) => Evaluate(run, ActivityExecutionContext.Current.CancellationToken);

    /// <summary>Evaluates each gate of <paramref name="run"/> once.</summary>
    /// <param name="run">The gates and what they evaluate.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>An answer per gate that has a runtime and returned; none for a gate without a runtime or one that threw.</returns>
    public Task<IReadOnlyList<GateAnswer>> Evaluate(GateRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(logger);
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException("GateActivities.Evaluate is engine P7 unit U3");
    }
}
