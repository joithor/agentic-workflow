using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.GateRerun;

/// <summary>Runs one test of a step in a tree and reads how it went. With filterMustMatch, a run that matched no test is an error.</summary>
/// <param name="runner">Runs the command.</param>
/// <param name="paths">Names the heavy-job wrapper.</param>
internal sealed class RerunExecutor(ICommandRunner runner, MetaPaths paths)
{
    private const int NoTestsRan = 8;
    private static readonly TimeSpan _limit = TimeSpan.FromMinutes(45);

    /// <summary>The command that runs one test of the step, or null when the step can't.</summary>
    internal IReadOnlyList<string>? Command(StepConfig step, string test) => GateCommand.Rerun(step, test, paths.Heavy);

    internal async ValueTask<Result<RerunOutcome>> Run(IReadOnlyList<string> argv, string tree, string test, bool filterMustMatch, CancellationToken cancellationToken)
    {
        var ran = await runner.Run(new CommandRequest(argv, _limit) { Cwd = tree, Env = GatePriority.Environment }, cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        var result = ran.Value;
        var output = result.Stdout + "\n" + result.Stderr;
        var ranNothing = TestNames.RanNone(output) || (result.ExitCode == NoTestsRan && !TestNames.Ran(output));
        if (!result.TimedOut && ranNothing && argv.Contains("dotnet", StringComparer.Ordinal))
        {
            // On the gate tree the test exists, so a filter that matches nothing is a bad filter. On main it is a test the PR adds.
            return filterMustMatch ? MetaErrors.Invalid.With($"no test matched '{test}'") : new RerunOutcome(Passed: false, "no test matched", Absent: true);
        }

        // Exit 8 with tests run is some project having none to run, as in a solution-wide filter: the tests that ran passed.
        return result is { ExitCode: 0 or NoTestsRan, TimedOut: false }
            ? new RerunOutcome(Passed: true)
            : new RerunOutcome(Passed: false, result.TimedOut ? "timed out" : FailureExcerpt.For(output, test));
    }

    /// <summary>The same command without --no-build, for a tree nobody built.</summary>
    internal static IReadOnlyList<string> Building(IReadOnlyList<string> argv) => [.. argv.Where(word => word != "--no-build")];
}
