namespace Aw.Sindri.Core.Workflow.Engine.Desk;

/// <summary>One open instance as 'workflow list' shows it (engine spec rev 8 §5.1 search attributes). Engine P4a.</summary>
/// <param name="Address">The instance address (the workflow id).</param>
/// <param name="State">The 'State' search attribute, when set.</param>
/// <param name="NeedsYou">The 'NeedsYou' search attribute: a desk run waits on a human.</param>
/// <param name="StartedAt">When the instance started (visibility's start time), when the listing knows it.</param>
public sealed record InstanceRow(string Address, string? State, bool NeedsYou, DateTimeOffset? StartedAt = null);
