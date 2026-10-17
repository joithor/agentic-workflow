using Aw.Kernel;
using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Gate;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Gate;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ParallelStepsProcessTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    private StepConfig Waiting(string mine, string theirs) =>
        new(mine, ["sh", "-c", $"touch '{_dir.Path}/{mine}'; i=0; while [ ! -e '{_dir.Path}/{theirs}' ]; do i=$((i+1)); [ $i -gt 200 ] && exit 1; sleep 0.05; done"], Heavy: true, After: []);

    [Fact, Trait("Lane", "integration")]
    public async Task Two_real_steps_that_wait_for_each_other_can_only_pass_when_they_run_at_the_same_time()
    {
        var steps = new[] { Waiting("a", "b"), Waiting("b", "a") };
        var graph = StepGraph.Build(steps, ["a", "b"]).Value;
        var runner = new SystemCommandRunner();

        var result = await StepScheduler.Run(
            graph,
            limit: 2,
            async (step, token) =>
            {
                var ran = await runner.Run(new CommandRequest(step.Run, TimeSpan.FromSeconds(30)), token);
                return ran.IsError ? ran.Error : Results.Success(ran.Value.ExitCode);
            },
            CancellationToken.None);

        Assert.Equal([0, 0], result.Value);
    }

    public void Dispose() => _dir.Dispose();
}
