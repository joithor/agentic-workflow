using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The answer commands of 'allowedCommands': one per variant that fits an open park or escalation.</summary>
public static partial class InstanceViews
{
    /// <summary>
    /// One command per answer variant that fits the suspension (the §3.8 row, the rule the answer check uses), the status line's
    /// own answer first with exactly its CLI, each other variant with its option spelled out.
    /// </summary>
    private static IEnumerable<AllowedCommand> AnswerCommands(WorkflowInstance instance, Suspension suspension, StatusAnswer? primary)
    {
        var shape = new ParkShape(suspension.Kind, suspension.Reason, suspension.ParkKind, suspension.Escalation);
        var line = primary ?? ParkAnswers.For(shape, instance.Address);
        var answering = $"sindri workflow answer {instance.Address.Value}";
        if (line is { Kind: not AnswerKind.CancelOnly })
        {
            yield return new(line.ActionId.Value, line.Cli);
        }

        foreach (var variant in Fitting(suspension))
        {
            var (kind, id, option) = variant switch   // Reject has no status-line kind: it is never the primary
            {
                AnswerVariant.Text => ((AnswerKind?)AnswerKind.Text, "answer-text", "--text <answer>"),
                AnswerVariant.Resume => (AnswerKind.Resume, "answer-resume", "--resume"),
                AnswerVariant.Grant => (AnswerKind.Grant, "answer-grant", "--grant 1"),
                AnswerVariant.GrantTokens => (AnswerKind.GrantTokens, "answer-grant-tokens", "--grant-tokens <n>"),
                AnswerVariant.Accept => (AnswerKind.Accept, "answer-accept", "--accept"),
                AnswerVariant.Reject or _ => (null, "answer-reject", "--reject"),
            };
            if (line is null || line.Kind != kind)
            {
                yield return new($"ApplyCommand:{id}", $"{answering} {option}");
            }
        }
    }

    /// <summary>
    /// The answer variants that fit a suspension, in the order spec §3.8 lists them. The same table as the answer check's
    /// (Rules.AnswerFit.Variants); Runtime can't reference Rules (ARCH-CYCLE-001), so a test pins the two to agree on every shape.
    /// </summary>
    /// <param name="suspension">The park or escalation.</param>
    /// <returns>The variants; empty when only cancel or an approval ends it.</returns>
    public static IReadOnlyList<AnswerVariant> Fitting(Suspension suspension)
    {
        ArgumentNullException.ThrowIfNull(suspension);
        return suspension.Kind == SuspensionKind.Escalation
            ? suspension.Escalation switch
            {
                EscalationKind.Gate => [AnswerVariant.Accept, AnswerVariant.Reject],
                EscalationKind.Timeout => [AnswerVariant.Resume],
                EscalationKind.Child or null or _ => [],
            }
            : suspension.Reason switch
            {
                ParkReason.Rounds => [AnswerVariant.Grant],
                ParkReason.NoProgress => [AnswerVariant.Grant, AnswerVariant.Text],
                ParkReason.Budget => [AnswerVariant.GrantTokens],
                ParkReason.NeedsAnswer => suspension.ParkKind == ParkKind.Wait ? [AnswerVariant.Resume] : [AnswerVariant.Text],
                ParkReason.Timeout or ParkReason.NoRuntime => [AnswerVariant.Resume],
                ParkReason.SpecConflict or _ => suspension.ParkKind == ParkKind.Dispatch ? [AnswerVariant.Resume] : [],
            };
    }
}
