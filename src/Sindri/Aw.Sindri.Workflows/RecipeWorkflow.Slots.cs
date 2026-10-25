using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The slot stub (workflow engine spec §12, engine P7 plan D8): until real session slots exist, a slot is always free, so the
/// interpreter grants one the moment an instance waits for it, as the resource allocator's 'wait.satisfied' with no outcome.
/// A wait is granted when it is entered and when a park on it is resumed (a refused grant waits again, §5.8), never while it
/// is parked, so a refusal can't spin. The only thing that holds an item back is the router ('workflow.route').
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private const string SlotStub = "service:slot.stub";
    private int _grants;

    private void GrantSlot(WorkflowInstance? before, WorkflowInstance instance, Recipe recipe)
    {
        if (!_children || instance.Ended || instance.Suspension is not null || recipe.States[instance.State].WaitFor?.Resource != ResourceKind.Slot)
        {
            return;
        }

        var entered = before is null || before.Frame.Entry != instance.Frame.Entry || !string.Equals(before.State, instance.State, StringComparison.Ordinal);
        if (entered || before!.Suspension is not null)   // why: a resume is the only other way to a wait with no suspension, and it follows a suspended before
        {
            _delivered.Enqueue(new WaitSatisfied(SlotStub, new ResourceWaitSource(ResourceKind.Slot, ++_grants), Outcome: null, new Dictionary<FactKey, FactValue>()));
        }
    }
}
