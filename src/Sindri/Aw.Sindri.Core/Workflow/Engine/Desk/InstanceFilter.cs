using Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

namespace Aw.Sindri.Core.Workflow.Engine.Desk;

/// <summary>
/// The typed filters of the Workflows list (UI rev 8; API spec §3.8 'GET /workflow/instances'). Each one given narrows
/// the list, and they AND together; <see cref="InstanceFilters.ToQuery"/> compiles them to a visibility query.
/// </summary>
/// <param name="Recipe">Only instances of this recipe (the 'Recipe' search attribute, the recipe id).</param>
/// <param name="State">Only instances in this state.</param>
/// <param name="NeedsYou">Only instances that wait on a human (true), or only those that don't (false).</param>
/// <param name="Urgency">Only instances of recipes with this urgency.</param>
/// <param name="Item">Only this item's instances.</param>
/// <param name="Subject">Only instances whose first input fact has this key ('fact:spec').</param>
/// <param name="Root">Only top-level instances (true), or only children (false).</param>
public sealed record InstanceFilter(
    string? Recipe = null,
    string? State = null,
    bool? NeedsYou = null,
    Urgency? Urgency = null,
    string? Item = null,
    string? Subject = null,
    bool? Root = null);
