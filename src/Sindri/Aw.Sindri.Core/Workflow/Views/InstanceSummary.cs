using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One row of the list (E-20).</summary>
/// <param name="Address">The CLI address.</param>
/// <param name="Instance">The engine's WFI id, which the id routes take.</param>
/// <param name="Item">The item id when the instance is an item's.</param>
/// <param name="Recipe">The recipe stamp.</param>
/// <param name="State">The current state id.</param>
/// <param name="Chip">The state chip: one word and its tone.</param>
/// <param name="Since">When it entered the state.</param>
/// <param name="EngineOwned">The badge that replaces the glyph.</param>
/// <param name="Subject">The subject, from the first input.</param>
/// <param name="Inputs">The inputs, in declaration order.</param>
/// <param name="States">Every state cell in path order.</param>
/// <param name="Now">The engine's next sentence, at most 80 characters.</param>
/// <param name="Answer">The answer the instance waits for, as on its status.</param>
/// <param name="Actions">The action ids allowed now.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstanceSummary(
    string Address,
    [property: JsonPropertyName("id")] string Instance,
    [property: JsonPropertyName("itemId")] string? Item,
    InstanceRecipe Recipe,
    string State,
    StateChip Chip,
    DateTimeOffset Since,
    EngineOwnedBadge? EngineOwned,
    SubjectRef Subject,
    IReadOnlyList<InputPresence> Inputs,
    IReadOnlyList<StateCell> States,
    string Now,
    StatusAnswer? Answer,
    IReadOnlyList<string> Actions);
