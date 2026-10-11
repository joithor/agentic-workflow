using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>A refused transition, for 'RefusalRules.Refuse' (spec §5.5).</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Recipe">Its recipe.</param>
/// <param name="Trigger">The refused trigger.</param>
/// <param name="Transition">The index of the refused transition.</param>
/// <param name="Gates">Every gate's record, in order (at least one refused or escalated, or an evidence check failed).</param>
/// <param name="Evidence">Every evidence check's record.</param>
public sealed record RefusalInput(
    WorkflowInstance Instance,
    Recipe Recipe,
    Trigger Trigger,
    int Transition,
    IReadOnlyList<GateRecord> Gates,
    IReadOnlyList<EvidenceRecord> Evidence);
