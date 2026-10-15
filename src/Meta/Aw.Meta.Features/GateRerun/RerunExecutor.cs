using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.GateRerun;

/// <summary>Runs one test of a step in a tree and reads how it went.</summary>
/// <param name="runner">Runs the command.</param>
/// <param name="paths">Names the heavy-job wrapper.</param>
internal sealed class RerunExecutor(ICommandRunner runner, MetaPaths paths)
{
    private const int NoTestsRan = 8;
    private static readonly TimeSpan _limit = TimeSpan.FromMinutes(45);

    /// <summary>The command that runs one test of the step, or null when the step can't.</summary>
    internal IReadOnlyList<string>? Command(StepConfig step, string test) => GateCommand.Rerun(step, test, paths.Heavy);

    internal async ValueTask<Result<RerunOutcome>> Run(IReadOnlyList<string> argv, string tree, string test, CancellationToken cancellationToken)
    {
        var ran = await runner.Run(new CommandRequest(argv, _limit) { Cwd = tree }, cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        var result = ran.Value;
        if (result is { ExitCode: NoTestsRan, TimedOut: false } && argv.Contains("dotnet", StringComparer.Ordinal))
        {
            return MetaErrors.Invalid.With($"no test matched '{test}'");
        }

        return result is { ExitCode: 0, TimedOut: false }
            ? new RerunOutcome(Passed: true)
            : new RerunOutcome(Passed: false, result.TimedOut ? "timed out" : FailureExcerpt.For(result.Stdout + "\n" + result.Stderr, test));
    }
}
