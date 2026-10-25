using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The head counts of the instance list (API spec §3.8, E-20), each from the rows' chips.</summary>
public static class InstanceCounts
{
    /// <summary>
    /// The counts over <paramref name="views"/>, which the caller passes as the whole root set before 'recipe' or 'open' narrows it:
    /// 'open' is every row that is not finished, 'needsYou' the PARKED, ESCALATED and DECISION rows, 'withYou' the WITH YOU rows,
    /// 'running' the RUNNING rows and 'done' the finished ones, whatever their outcome.
    /// </summary>
    /// <param name="views">The root instances, ended ones included.</param>
    /// <returns>The counts.</returns>
    public static ListCounts Of(IEnumerable<InstanceView> views)
    {
        ArgumentNullException.ThrowIfNull(views);
        int open = 0, needsYou = 0, withYou = 0, running = 0, done = 0;
        foreach (var view in views)
        {
            var chip = StateChip.For(view.EngineOwned, deskOpen: view.Task is not null, view.Outcome, view.State == InstanceViews.StartingState);
            if (StateChip.IsFinished(chip.Word))
            {
                done++;
                continue;
            }

            switch (chip.Word)
            {
                case StateChip.Parked or StateChip.Escalated or StateChip.Decision:
                    needsYou++;
                    break;
                case StateChip.WithYou:
                    withYou++;
                    break;
                case StateChip.Running:
                    running++;
                    break;
                default:
                    break;   // QUEUED: open, in no other count
            }

            open++;
        }

        return new ListCounts(open, needsYou, withYou, running, done);
    }
}
