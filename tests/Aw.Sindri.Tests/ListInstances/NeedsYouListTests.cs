using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>'workflow list --needs-me' (engine spec §6.3, README X5): Decisions, then Tasks; a parent mirroring a listed child once, under the child.</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouListTests
{
    [Fact]
    public void Parks_and_escalations_are_decisions_and_desk_tasks_are_tasks_each_by_address()
    {
        var task = StatusCases.View("desk-task") with { Address = InstanceAddress.From("T-9/step/1") };
        var park = StatusCases.View("parked-rounds");
        var escalation = StatusCases.View("escalated-gate") with { Address = InstanceAddress.From("T-1/step/1") };

        var list = NeedsYouList.Of([task, park, escalation, StatusCases.View("done") with { Address = InstanceAddress.From("T-0/step/1") }]);

        Assert.Equal(["T-1/step/1", "T-3/step/1"], list.Decisions.Select(static v => v.Address.Value), StringComparer.Ordinal);
        Assert.Equal(["T-9/step/1"], list.Tasks.Select(static v => v.Address.Value), StringComparer.Ordinal);
        Assert.False(list.IsEmpty);
    }

    [Fact]
    public void A_parent_mirroring_a_listed_child_is_shown_once_under_the_child()
    {
        var parent = StatusCases.View("parked-no-progress-child");
        var child = StatusCases.View("parked-rounds") with { Address = InstanceAddress.From("T-3/step/1/step.4") };

        var list = NeedsYouList.Of([parent, child]);

        Assert.Equal(["T-3/step/1/step.4"], list.Decisions.Select(static v => v.Address.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void Nothing_needs_you_is_the_empty_state() =>
        Assert.Equal((true, "nothing needs you"), (NeedsYouList.Of([]).IsEmpty, NeedsYouList.Nothing));
}
