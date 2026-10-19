using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>What a failed launch does to the run (engine P8 D8, launcher L-6).</summary>
[Lane(TestLane.Unit)]
public sealed class AgentLaunchFailuresTests
{
    [Fact]
    public void Not_approved_argv_parks_with_SND_LAUNCH_012()
    {
        var failure = AgentLaunchFailures.Of(new FailedLaunch(SindriErrors.LaunchNotApproved.With("not-approved: aider"), LaunchStage.Host));

        Assert.Equal((AgentLaunchFailureKind.Park, "SND-LAUNCH-012"), (failure.Kind, failure.Code));
        Assert.Contains("SND-LAUNCH-012", failure.Text, StringComparison.Ordinal);
        Assert.Contains("not-approved: aider", failure.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_form_the_entry_lacks_parks_with_SND_LAUNCH_013()
    {
        var failure = AgentLaunchFailures.Of(new FailedLaunch(SindriErrors.LaunchFormUnsupported.With("policy form is none"), LaunchStage.Host));

        Assert.Equal((AgentLaunchFailureKind.Park, "SND-LAUNCH-013"), (failure.Kind, failure.Code));
    }

    [Fact]
    public void A_host_failure_is_step_failed_launch()
    {
        var failure = AgentLaunchFailures.Of(new FailedLaunch(SindriErrors.LaunchInterrupted.With("tmux refused"), LaunchStage.Host));

        Assert.Equal(AgentLaunchFailureKind.StepFailed, failure.Kind);
        Assert.StartsWith("launch", failure.Text, StringComparison.Ordinal);
        Assert.Contains("tmux refused", failure.Text, StringComparison.Ordinal);
    }
}
