using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>The Cancel confirmation sentence (owner decision 2026-10-14).</summary>
public sealed class ConfirmTextTests
{
    [Fact, Trait("Lane", "unit")]
    public void Cancel_names_the_address_the_running_steps_and_the_children() =>
        Assert.Equal("Cancel ITM-01JK1/incident-response/1? 1 running steps stop; 1 children are cancelled.", ConfirmText.Cancel("ITM-01JK1/incident-response/1", 1, 1));

    [Fact, Trait("Lane", "unit")]
    public void Only_cancel_carries_a_confirmation()
    {
        var commands = WorkflowViewFixtures.StatusParkedChild().Commands;

        Assert.Equal(ConfirmText.Cancel("ITM-01JKD/project/1", 0, 1), Assert.Single(commands, c => c.Verb.Value == "cancel").Confirm);
        Assert.All(commands.Where(c => c.Verb.Value != "cancel"), c => Assert.Null(c.Confirm));
    }
}
