using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.GateRun;

/// <summary>Runs one gate step in the gate tree, its output going to the step's log.</summary>
/// <param name="runner">Runs the step's command.</param>
/// <param name="paths">Names the heavy-job wrapper.</param>
internal sealed class GateStepRunner(ICommandRunner runner, MetaPaths paths)
{
    private static readonly TimeSpan _limit = TimeSpan.FromHours(2);

    internal async ValueTask<Result<GateStep>> Run(StepConfig step, string tree, string log, CancellationToken cancellationToken)
    {
        var request = new CommandRequest(GateCommand.For(step, paths.Heavy), _limit) { Cwd = tree, LogFile = log, Env = GatePriority.Environment };
        var ran = await runner.Run(request, cancellationToken).ConfigureAwait(false);
        return ran.IsError
            ? ran.Error
            : new GateStep(step.Name, ran.Value is { ExitCode: 0, TimedOut: false } ? GateVerdict.Pass : GateVerdict.Fail, log);
    }
}
