using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The typed code of a park (q65), served as 'reasonCode' on the status where the engine knows one, so the app never parses the reason's words.</summary>
[Lane(TestLane.Unit)]
public sealed class ParkReasonCodeTests
{
    [Fact]
    public void A_refused_dispatch_park_serves_the_code_it_was_made_with()
    {
        var parked = StatusCases.Instance("parked-dispatch-no-runtime");
        parked = parked with { Suspension = parked.Suspension! with { Code = "SND-WF-010" } };

        Assert.Equal("SND-WF-010", StatusOf(parked).ReasonCode);
    }

    [Fact]
    public void An_unavailable_park_serves_the_code_of_the_dependency_that_was_down() =>
        Assert.Equal("SND-LAUNCH-007", StatusOf(StatusCases.Instance("parked-unavailable-step")).ReasonCode);

    [Fact]
    public void An_unavailable_park_without_a_dependency_code_serves_none()
    {
        var parked = StatusCases.Instance("parked-unavailable-step");

        Assert.Null(StatusOf(parked with { Suspension = parked.Suspension! with { Code = null } }).ReasonCode);
    }

    [Fact]
    public void A_step_with_no_runtime_serves_SND_WF_010_without_a_stored_code() =>
        Assert.Equal("SND-WF-010", StatusOf(StatusCases.Instance("parked-no-runtime")).ReasonCode);

    [Theory]
    [InlineData("parked-needs-answer-step")]
    [InlineData("parked-budget")]
    [InlineData("parked-dispatch-allowlist")]
    [InlineData("escalated-gate")]
    [InlineData("taken-over")]
    [InlineData("desk-task")]
    public void A_park_the_engine_gives_no_code_and_a_state_that_is_no_park_serve_none(string name) =>
        Assert.Null(StatusOf(StatusCases.Instance(name)).ReasonCode);

    [Fact]
    public void The_code_is_written_in_the_json_as_reasonCode_and_left_out_when_there_is_none()
    {
        var parked = StatusOf(StatusCases.Instance("parked-no-runtime"));
        var none = StatusOf(StatusCases.Instance("parked-budget"));

        Assert.Contains("\"reasonCode\":\"SND-WF-010\"", System.Text.Json.JsonSerializer.Serialize(parked, SliceHarness.Json), StringComparison.Ordinal);
        Assert.DoesNotContain("reasonCode", System.Text.Json.JsonSerializer.Serialize(none, SliceHarness.Json), StringComparison.Ordinal);
    }

    private static StatusFields StatusOf(WorkflowInstance instance) =>
        InstanceViews.Status(InstanceViews.Of(instance, StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext()), StatusCases.Now);
}
