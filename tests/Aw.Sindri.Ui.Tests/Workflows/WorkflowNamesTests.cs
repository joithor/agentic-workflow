using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The name a person sees for a workflow (F4): the served display name, or the id made readable. The raw id is never the name.</summary>
[Lane(TestLane.Unit)]
public sealed class WorkflowNamesTests
{
    [Fact]
    public void A_served_display_name_is_shown_as_served() =>
        Assert.Equal("Build and ship a change", WorkflowNames.Of("workflow.build", "Build and ship a change"));

    [Theory]
    [InlineData("workflow.scoping", "Scoping")]
    [InlineData("workflow.build", "Build")]
    [InlineData("workflow.bug-fix", "Bug Fix")]
    [InlineData("workflow.triage_alert", "Triage Alert")]
    [InlineData("workflow.dev.desk-round-trip", "Dev Desk Round Trip")]
    [InlineData("triage-alert", "Triage Alert")]
    public void Without_a_display_name_the_id_is_title_cased_without_its_workflow_prefix(string id, string expected) =>
        Assert.Equal(expected, WorkflowNames.Of(id, displayName: null));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_display_name_counts_as_missing(string blank) =>
        Assert.Equal("Scoping", WorkflowNames.Of("workflow.scoping", blank));

    [Theory]
    [InlineData("workflow.scoping", "workflow.scoping")]
    [InlineData("workflow.scoping", " workflow.scoping ")]
    public void A_display_name_that_is_the_recipe_id_counts_as_missing(string id, string served) =>
        Assert.Equal("Scoping", WorkflowNames.Of(id, served));

    [Fact]
    public void An_id_that_is_only_the_prefix_stays_something_to_show() =>
        Assert.Equal("Workflow", WorkflowNames.Of("workflow", displayName: null));

    [Theory]
    [InlineData("fact:scopeInput", "Scope input")]
    [InlineData("fact:spec", "Spec")]
    [InlineData("prd_link", "Prd link")]
    [InlineData("start:date", "Date")]
    public void An_input_without_a_label_is_named_from_its_key_in_plain_words(string key, string expected) =>
        Assert.Equal(expected, WorkflowNames.InputName(key));
}
