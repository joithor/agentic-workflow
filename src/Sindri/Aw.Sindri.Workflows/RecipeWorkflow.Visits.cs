using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

internal sealed partial class RecipeWorkflow
{
    private readonly Dictionary<string, StateVisit> _visits = new(StringComparer.Ordinal);   // the latest visit to each state, for the state band's times; built from the workflow's own clock, so a replay rebuilds it

    /// <summary>Records that the instance left the frame's state it was in (when it moved to another) and entered <paramref name="after"/>'s: the latest visit to a state is the one the band shows.</summary>
    private void Visited(WorkflowInstance? before, WorkflowInstance after)
    {
        var (was, entered) = (before?.Frame, after.Frame);   // the frame's state is the recipe's: a park or takeover changes State, not the frame
        if (was is not null && was.State == entered.State && was.Entry == entered.Entry)
        {
            return;
        }

        if (was is not null && !string.Equals(was.State, entered.State, StringComparison.Ordinal) && _visits.TryGetValue(was.State, out var left))
        {
            _visits[was.State] = left with { LeftAt = Now };
        }

        _visits[entered.State] = new StateVisit(Now);
    }
}
