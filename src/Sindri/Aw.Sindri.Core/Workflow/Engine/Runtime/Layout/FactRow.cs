namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>One input or output the recipe declares (E-20).</summary>
/// <param name="Key">The fact key.</param>
/// <param name="Label">The label.</param>
/// <param name="Type">text, file, item, link, scope-map, packet, evidence, decision or payload; an unknown type is served as payload.</param>
/// <param name="Direction">Input or output.</param>
/// <param name="From">The state that produces it.</param>
/// <param name="Status">How far it has come.</param>
/// <param name="Summary">The one-line summary.</param>
/// <param name="At">When it was produced.</param>
/// <param name="By">By whom: you, a Step id or an agent.</param>
/// <param name="Ref">The per-type handle.</param>
/// <param name="Progress">Progress for an evidence fact.</param>
/// <param name="Pending">What the complete form needs, on a pending output of an open desk state.</param>
public sealed record FactRow(
    string Key,
    string Label,
    string Type,
    FactDirection Direction,
    string? From,
    FactStatus Status,
    string Summary,
    DateTimeOffset? At,
    string? By,
    FactRef? Ref,
    ChildProgress? Progress,
    PendingOutput? Pending);
