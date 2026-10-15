using Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

namespace Aw.Sindri.Core.Workflow.Engine.Desk;

/// <summary>One open instance as 'workflow list' shows it (engine spec rev 8 §5.1 search attributes). Engine P4a.</summary>
/// <param name="Address">The instance address (the workflow id).</param>
/// <param name="State">The 'State' search attribute, when set.</param>
/// <param name="NeedsYou">The 'NeedsYou' search attribute: a desk run waits on a human.</param>
/// <param name="StartedAt">When the instance started (visibility's start time), when the listing knows it.</param>
/// <param name="Recipe">The 'Recipe' search attribute: the recipe id, when set.</param>
/// <param name="Urgency">The 'Urgency' search attribute; <see cref="Runtime.Layout.Urgency.Work"/> when unset.</param>
/// <param name="Subject">The 'Subject' search attribute: the key of the first input fact, when the recipe declares one.</param>
/// <param name="Root">The 'Root' search attribute: a top-level instance, not a child; true when unset.</param>
public sealed record InstanceRow(
    string Address,
    string? State,
    bool NeedsYou,
    DateTimeOffset? StartedAt = null,
    string? Recipe = null,
    Urgency Urgency = Urgency.Work,
    string? Subject = null,
    bool Root = true);
