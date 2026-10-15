using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Workflows;
using Temporalio.Common;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>The row of an execution from its search attributes: an attribute never set reads as its default, as the typed filters match it.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceRowTests
{
    private static readonly DateTime _started = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void An_execution_with_no_attributes_is_a_work_root_with_no_subject_that_needs_nobody()
    {
        var row = TemporalWorkflowInstances.Row("T-1/step/1", _started, SearchAttributeCollection.Empty);

        Assert.Equal(new InstanceRow("T-1/step/1", State: null, NeedsYou: false, new DateTimeOffset(_started, TimeSpan.Zero), Recipe: null, RecipeUrgency.Work, Subject: null, Root: true), row);
    }

    [Fact]
    public void The_attributes_the_workflow_set_are_the_rows_fields()
    {
        var attributes = new SearchAttributeCollection.Builder()
            .Set(SindriTemporal.StateKey, "parked")
            .Set(SindriTemporal.NeedsYouKey, value: true)
            .Set(SindriTemporal.RecipeKey, "workflow.incident")
            .Set(SindriTemporal.UrgencyKey, "incident")
            .Set(SindriTemporal.SubjectKey, "fact:spec")
            .Set(SindriTemporal.RootKey, value: false)
            .ToSearchAttributeCollection();

        var row = TemporalWorkflowInstances.Row("T-1/step/1", _started, attributes);

        Assert.Equal(new InstanceRow("T-1/step/1", "parked", NeedsYou: true, new DateTimeOffset(_started, TimeSpan.Zero), "workflow.incident", RecipeUrgency.Incident, "fact:spec", Root: false), row);
    }

    [Theory]
    [InlineData("urgent")]
    [InlineData("Incident")]
    [InlineData("")]
    public void An_urgency_this_build_does_not_know_reads_as_work(string word)
    {
        var attributes = new SearchAttributeCollection.Builder().Set(SindriTemporal.UrgencyKey, word).ToSearchAttributeCollection();

        Assert.Equal(RecipeUrgency.Work, TemporalWorkflowInstances.Row("T-1/step/1", _started, attributes).Urgency);
    }
}
