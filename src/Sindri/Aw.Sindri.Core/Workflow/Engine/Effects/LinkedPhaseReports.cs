using System.Text.Json;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>
/// What a linked instance (a phase started by 'effect.start-phase', LN-WF1) tells its line when it parks, resumes or ends: one
/// engine-origin 'effect.line.report' with status 'parked', 'resumed' or 'ended'. The report's reply is 'line.phaseChanged', whose
/// causer is the linked instance, so the event bridge routes it to the line (route: parent). A phase that parks and resumes twice
/// reports four times: the identity is the change's (<see cref="EffectIdentity.ForLineReport"/>).
/// </summary>
public static class LinkedPhaseReports
{
    private static readonly BlockId _report = BlockId.From("effect.line.report");
    private static readonly FactKey _lineContext = FactKey.From("fact:lineContext");
    private static readonly FactKey _phasePacket = FactKey.From("fact:phasePacket");

    /// <summary>The report a change from <paramref name="before"/> to <paramref name="after"/> sends the line; none for an instance that isn't linked or a change the line doesn't track.</summary>
    /// <param name="before">The instance before the change (null at start).</param>
    /// <param name="after">The instance after it.</param>
    /// <param name="version">The instance's version after the change.</param>
    /// <returns>The command, or null.</returns>
    public static EffectCommand? For(WorkflowInstance? before, WorkflowInstance after, int version)
    {
        ArgumentNullException.ThrowIfNull(after);
        if (after.Address.LinkedParent() is null || !after.Facts.ContainsKey(_lineContext) || Status(before, after) is not { } status)
        {
            return null;
        }

        var with = new Dictionary<string, WithValue>(StringComparer.Ordinal)
        {
            ["status"] = Literal(status),
            ["line"] = new WithValue(Literal: null, _lineContext, Profile: null),
        };
        if (after.Facts.ContainsKey(_phasePacket))
        {
            with["phasePacket"] = new WithValue(Literal: null, _phasePacket, Profile: null);   // the report reads the plan's phase id ("P1") from it, not from the lowercase address slug
        }

        Extras(status, after, with);
        return new EffectCommand(_report, EffectIdentity.ForLineReport(after.Id, after.Frame.Entry, version), CommandOrigin.Engine, with);
    }

    private static void Extras(string status, WorkflowInstance after, Dictionary<string, WithValue> with)
    {
        if (status == "parked")
        {
            with["parkReason"] = Literal(EngineNotices.Reason(after.Suspension?.Reason));
        }
        else if (status == "ended")
        {
            with["ended"] = Literal(after.Outcome?.Value ?? string.Empty);
            // why: amendments (LN-PKT1) don't exist yet, so the packet in force is the one the phase started with
            if (after.Facts.TryGetValue(_phasePacket, out var packet))
            {
                with["packet"] = Literal(packet.Sha256);
            }
        }
    }

    private static string? Status(WorkflowInstance? before, WorkflowInstance after)
    {
        if (after.Ended)
        {
            return before is { Ended: true } ? null : "ended";
        }

        var (was, now) = (Open(before?.Suspension), Open(after.Suspension));
        return now is not null ? (was == now ? null : "parked") : was is not null ? "resumed" : null;
    }

    /// <summary>The suspension a line sees as a pause: a park or an escalation; a takeover is a human at the keyboard, not a parked phase.</summary>
    private static Suspension? Open(Suspension? suspension) => suspension is { Kind: SuspensionKind.Park or SuspensionKind.Escalation } ? suspension : null;

    private static WithValue Literal(string text)
    {
        using var document = JsonDocument.Parse("\"" + JsonEncodedText.Encode(text).Value + "\"");
        return new WithValue(document.RootElement.Clone(), Fact: null, Profile: null);
    }
}
