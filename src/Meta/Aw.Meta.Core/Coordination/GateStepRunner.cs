using Aw.Kernel;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Runs one gate step in the gate tree, its output going to the step's log.</summary>
/// <param name="runner">Runs the step's command.</param>
/// <param name="paths">Names the heavy-job wrapper.</param>
public sealed class GateStepRunner(ICommandRunner runner, MetaPaths paths)
{
    private static readonly TimeSpan _limit = TimeSpan.FromHours(2);

    /// <summary>Runs the step.</summary>
    /// <param name="step">The step.</param>
    /// <param name="tree">The gate tree to run in.</param>
    /// <param name="log">The log file.</param>
    /// <param name="cancellationToken">Cancels the step (its process tree is killed).</param>
    /// <returns>The step with its verdict, or the error that kept it from starting.</returns>
    public async ValueTask<Result<GateStep>> Run(StepConfig step, string tree, string log, CancellationToken cancellationToken)
    {
        var request = new CommandRequest(GateCommand.For(step, paths.Heavy), _limit) { Cwd = tree, LogFile = log, Env = GatePriority.Environment, UnsetEnv = GatePriority.Scrubbed };
        var ran = await runner.Run(request, cancellationToken).ConfigureAwait(false);
        return ran.IsError
            ? ran.Error
            : new GateStep(step.Name, ran.Value is { ExitCode: 0, TimedOut: false } ? GateVerdict.Pass : GateVerdict.Fail, log);
    }
}
