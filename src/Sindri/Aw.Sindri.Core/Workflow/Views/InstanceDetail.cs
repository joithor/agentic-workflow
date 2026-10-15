using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One instance for the Workflows frame (API spec section 3.8, E-20, E-23); the spec calls it InstanceView, a name the engine's status view holds.</summary>
/// <param name="Address">The CLI address.</param>
/// <param name="Instance">The engine's WFI id.</param>
/// <param name="Parent">The parent instance, for a child.</param>
/// <param name="Item">The item id.</param>
/// <param name="Title">The title.</param>
/// <param name="Recipe">The recipe stamp.</param>
/// <param name="State">The current state.</param>
/// <param name="Since">When it entered the state.</param>
/// <param name="EngineOwned">The badge.</param>
/// <param name="Subject">The subject.</param>
/// <param name="OrderedBy">How the states were ordered.</param>
/// <param name="States">Every state in path order.</param>
/// <param name="Current">The expanded state.</param>
/// <param name="Facts">Inputs then outputs.</param>
/// <param name="Children">Child groups in path order.</param>
/// <param name="Counts">The tab counts.</param>
/// <param name="Actions">The action ids allowed now.</param>
/// <param name="AsOf">When it was read.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstanceDetail(
    string Address,
    [property: JsonPropertyName("id")] string Instance,
    ParentRef? Parent,
    [property: JsonPropertyName("itemId")] string? Item,
    string Title,
    InstanceRecipe Recipe,
    string State,
    DateTimeOffset Since,
    EngineOwnedBadge? EngineOwned,
    SubjectRef Subject,
    PathOrdering OrderedBy,
    IReadOnlyList<StateCell> States,
    CurrentState Current,
    IReadOnlyList<FactRow> Facts,
    IReadOnlyList<ChildGroup> Children,
    ViewCounts Counts,
    IReadOnlyList<string> Actions,
    DateTimeOffset AsOf);
