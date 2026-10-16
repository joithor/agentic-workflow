using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>Runs the vetted CLI calls in order, each in its own temporary directory with an exact environment, and checks each expectation.</summary>
internal sealed class CallRunner(ICliRunner runner)
{
    internal async ValueTask RunAsync(VettedRun run, Collected collected, CancellationToken cancellationToken)
    {
        foreach (var call in run.Calls)
        {
            var info = new StepInfo(collected.Steps.Count + 1, call.Target, ApiAction.Cli, []);
            if (collected.Error is not null)
            {
                collected.Steps.Add(new ApiStep(info.Index, info.Action, info.Target, ApiStepStatus.Skipped, Code: null, Output: null, []));
                continue;
            }

            var invocation = new CliInvocation(call.Executable, [.. call.Step.Argv.Skip(1)], call.Step.Stdin, run.CliEnvironment);
            var outcome = await runner.RunAsync(invocation, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                collected.Fail(outcome.Error);
                collected.Steps.Add(new ApiStep(info.Index, info.Action, info.Target, ApiStepStatus.Broken, Code: null, Output: null, []));
                continue;
            }

            collected.Record(info, call.Step.Expect, outcome.Value.ExitCode, outcome.Value.Stdout, call.GoldenText);
        }
    }
}
