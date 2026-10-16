namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One row of the instance list.</summary>
/// <param name="Address">Address, as served.</param>
/// <param name="Id">Id, as served.</param>
/// <param name="ItemId">ItemId, as served.</param>
/// <param name="Recipe">Recipe, as served.</param>
/// <param name="State">State, as served.</param>
/// <param name="Chip">Chip, as served.</param>
/// <param name="Since">Since, as served.</param>
/// <param name="EngineOwned">EngineOwned, as served.</param>
/// <param name="Subject">Subject, as served.</param>
/// <param name="Inputs">Inputs, as served.</param>
/// <param name="States">States, as served.</param>
/// <param name="Now">Now, as served.</param>
/// <param name="Answer">Answer, as served.</param>
/// <param name="Actions">Actions, as served.</param>
internal sealed record InstanceSummary(
    string Address,
    string Id,
    string? ItemId,
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
