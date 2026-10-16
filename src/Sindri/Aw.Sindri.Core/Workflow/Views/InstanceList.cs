using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The workflow list (API spec section 3.8, E-20), ordered as InstanceOrder orders it: unfinished first, not parked or escalated first, urgency, subject label, address.</summary>
/// <param name="Instances">The rows.</param>
/// <param name="Counts">The head counts over the root set, before the open filter.</param>
/// <param name="Empty">The empty-state sentence, null when there are rows.</param>
/// <param name="AsOf">When the view was read.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstanceList(
    IReadOnlyList<InstanceSummary> Instances,
    ListCounts Counts,
    string? Empty,
    DateTimeOffset AsOf)
{
    /// <summary>The sentence <see cref="Empty"/> holds when there are no rows.</summary>
    public const string EmptyText = "No workflows are open.";

    /// <summary>The sentence <see cref="Empty"/> holds when a list that isn't limited to the open instances has no rows.</summary>
    public const string NoMatchText = "No workflows match.";
}
