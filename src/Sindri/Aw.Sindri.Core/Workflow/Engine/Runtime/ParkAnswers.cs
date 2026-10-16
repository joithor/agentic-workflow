using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Views;
using ActionClass = Aw.Sindri.Core.Commands.ActionClass;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The answer an open park or escalation takes (engine spec section 5.8, API spec section 3.8): one rule, shared by the status
/// layout (<see cref="InstanceWords"/>) and the needs-you rows, so a row's control is the one the status prints. The needs-you fold has no
/// recipe, so for a budget park it passes a placeholder token grant: the row's kind and action id are right, its amount is not the half-budget the status shows.
/// </summary>
public static class ParkAnswers
{
    /// <summary>The answer a suspension takes.</summary>
    /// <param name="shape">The suspension's kind, reason, park kind and escalation.</param>
    /// <param name="address">The instance's address.</param>
    /// <param name="grantTokens">The token amount a budget park's grant line offers.</param>
    /// <returns>The answer, or null when the suspension takes none (a drift, a revoked child or a takeover).</returns>
    public static StatusAnswer? For(ParkShape shape, InstanceAddress address, string grantTokens = "<n>")
    {
        ArgumentNullException.ThrowIfNull(shape);
        var answering = $"sindri workflow answer {address.Value}";
        return shape switch
        {
            { Kind: SuspensionKind.Park, Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Drift or ParkKind.ChildRevoked } => null,
            { Kind: SuspensionKind.Park, Reason: ParkReason.SpecConflict, ParkKind: ParkKind.Revoked } => Line(AnswerKind.CancelOnly, $"sindri workflow cancel {address.Value}"),
            { Kind: SuspensionKind.Park } => Park(shape, answering, grantTokens),
            { Kind: SuspensionKind.Escalation, Escalation: EscalationKind.Gate } => Line(AnswerKind.Accept, $"{answering} --accept", atTerminal: true),
            { Kind: SuspensionKind.Escalation, Escalation: EscalationKind.Timeout } => Line(AnswerKind.Resume, $"{answering} --resume"),
            { Kind: SuspensionKind.Escalation } => Line(AnswerKind.Text, $"{answering} <the child's answer>"),
            _ => null,
        };
    }

    private static StatusAnswer Park(ParkShape park, string answering, string grantTokens) => park.Reason switch
    {
        ParkReason.Rounds or ParkReason.NoProgress => Line(AnswerKind.Grant, $"{answering} --grant 1", atTerminal: true),
        ParkReason.Budget => Line(AnswerKind.GrantTokens, $"{answering} --grant-tokens {grantTokens}", atTerminal: true),
        ParkReason.NeedsAnswer when park.ParkKind == ParkKind.Wait => Line(AnswerKind.Resume, $"{answering} --resume [--text <answer>]"),
        ParkReason.NeedsAnswer => Line(AnswerKind.Text, $"{answering} --text <answer>"),
        ParkReason.Timeout or ParkReason.SpecConflict or ParkReason.NoRuntime or null or _ => Line(AnswerKind.Resume, $"{answering} --resume"),
    };

    private static StatusAnswer Line(AnswerKind kind, string cli, bool atTerminal = false) => new(
        kind,
        ActionId.From(kind switch
        {
            AnswerKind.Text => "ApplyCommand:answer-text",
            AnswerKind.Resume => "ApplyCommand:answer-resume",
            AnswerKind.Grant => "ApplyCommand:answer-grant",
            AnswerKind.GrantTokens => "ApplyCommand:answer-grant-tokens",
            AnswerKind.Accept => "ApplyCommand:answer-accept",
            AnswerKind.CancelOnly or _ => "ApplyCommand:cancel",
        }),
        kind is AnswerKind.Grant or AnswerKind.GrantTokens or AnswerKind.Accept ? ActionClass.Protected : ActionClass.Desk,
        cli,
        atTerminal);
}
