using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// An agent Step whose session ends with no report, with its result on disk (issue #1000): the run completes from the output folder, or parks with the folder named; with nothing there it fails as it
/// always did. 'step.report.write' reports 'done' with 'fact:resolution'.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(nameof(WallClockAlone))]
public sealed class AgentRecoveryRuntimeTests : AgentRuntimeTestBase
{
    [Theory(Timeout = 120_000)]
    [InlineData(SessionEndReason.Idle)]
    [InlineData(SessionEndReason.Lost)]
    [InlineData(SessionEndReason.Exit)]
    public async Task A_session_that_ended_with_its_result_on_disk_completes_the_run(SessionEndReason ended)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _outputs.Add(rig.Worktree, "resolution", "The button handler was missing.");
            _sessions.End(rig.Launch, ended, exitCode: ended == SessionEndReason.Exit ? 0 : null);

            var done = await WorkflowsHarness.Until(handle, static v => v.State == "done");

            Assert.Equal("done", done.State);
            Assert.Null(done.Question);
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_session_that_ended_with_files_that_prove_nothing_parks_naming_the_output_folder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _outputs.Add(rig.Worktree, "scratch.txt", "half a thought");
            _sessions.End(rig.Launch, SessionEndReason.Idle);

            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.Contains("the agent finished but its result was not recorded: " + Path.Combine(rig.Worktree, ".sindri-out"), parked.Question?.Question, StringComparison.Ordinal);
            Assert.Equal("idle", await rig.FailureReason(ct));
        });
    }

    [Fact(Timeout = 120_000)]
    public async Task A_session_that_ended_with_an_empty_folder_fails_with_its_own_end()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var rig = await Rig(ct);
        await rig.Run(async handle =>
        {
            _ = await WorkflowsHarness.Until(handle, static v => v.Run is not null && v.State == "write");
            await Launched(ct);
            _sessions.End(rig.Launch, SessionEndReason.Idle);

            var parked = await WorkflowsHarness.Until(handle, static v => v.Question is not null);

            Assert.DoesNotContain("not recorded", parked.Question?.Question, StringComparison.Ordinal);
            Assert.Equal("idle", await rig.FailureReason(ct));
        });
    }
}
