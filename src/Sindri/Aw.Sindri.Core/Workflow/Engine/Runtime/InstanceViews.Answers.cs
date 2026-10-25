using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The answer commands of 'allowedCommands': one per variant that fits an open park or escalation.</summary>
public static partial class InstanceViews
{
    /// <summary>
    /// One command per answer variant that fits the suspension (the §3.8 table the answer check uses), the status line's
    /// own variant first, each with its catalog id and the full command that runs it. A suspension that mirrors a child
    /// forwards the answer to it, so only variants the reason alone fixes are offered; anything the child decides (its park
    /// kind, its budget, its own escalation) is left to the status line.
    /// </summary>
    private static IEnumerable<AllowedCommand> AnswerCommands(WorkflowInstance instance, Suspension suspension, string grantTokens)
    {
        if (suspension.Mirrors is not null && !ReasonFixesChildFit(suspension))
        {
            yield break;
        }

        foreach (var variant in AnswerVariants.Fitting(suspension))
        {
            if (variant == AnswerVariant.GrantTokens && !TokenCounts.TryParse(grantTokens, out _))
            {
                continue;   // no cap to halve: the amount is the person's, so no entry carries a placeholder (the status line keeps its hint)
            }

            var row = AnswerRow.For(variant, grantTokens);
            yield return new(row.Action, row.Cli(instance.Address.Value));
        }
    }

    private static bool ReasonFixesChildFit(Suspension suspension) =>
        suspension.Kind == SuspensionKind.Park && AnswerVariants.ReasonFixesChildFit(suspension.Reason);
}
