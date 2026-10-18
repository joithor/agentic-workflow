using Aw.Kernel.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Engine spec rev 8.1 "Phase 4", signal 1: with 'workflow.step' approved, 'sindri workflow start workflow.step --item
/// T-1 --fact fact:spec=spec.md', then 'sindri workflow status T-1/step/1', shows the desk-task layout (§6.4) with
/// '--run 1.1'. Red at the P4 contract: 'start' (unit (b)) and 'status' (unit (e)) are stubs.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class PhaseFourSignalTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task Start_workflow_step_on_an_item_then_status_shows_the_desk_task_with_run_1_1()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, TestContext.Current.CancellationToken);
        var sindri = served.Sindri;
        {
            var spec = _harness.File("spec.md", "the spec");

            var (startExit, startOut, startErr) = await WorkflowsHarness.Cli(sindri, "start", "workflow.step", "--item", "T-1", "--fact", "fact:spec=" + spec);
            var (statusExit, statusOut, statusErr) = await WorkflowsHarness.Cli(sindri, "status", "T-1/step/1");

            Assert.Equal((0, string.Empty), (startExit, startErr));
            Assert.StartsWith("T-1/step/1  workflow.step@", startOut, StringComparison.Ordinal);
            Assert.Equal((0, string.Empty), (statusExit, statusErr));
            var lines = statusOut.Split('\n');
            Assert.Matches(@"^T-1/step/1  workflow\.step@[0-9a-f]{12}  v\d+  produce: desk task  since \d\d:\d\d \(.+\)$", lines[0]);
            Assert.Contains(lines, static l => l.Contains("sindri workflow complete T-1/step/1 --run 1.1 --outcome done --output fact:output=<file>", StringComparison.Ordinal));
            Assert.Contains(lines, static l => l.StartsWith("  inputs    fact:spec  spec.md  sha256:", StringComparison.Ordinal));
            Assert.Contains("  commands  complete, takeover, cancel", lines, StringComparer.Ordinal);
        }
    }
}
