namespace Aw.Sindri.Core.Workflow.Engine.Runtime.Layout;

/// <summary>One row of the instance list (UI screens rev 8 E-20; API spec 3.8): everything the Workflows page and the Home strip draw without a second call.</summary>
/// <param name="Address">The CLI address.</param>
/// <param name="ItemId">The item id when the instance is an item's.</param>
/// <param name="Recipe">The recipe it runs.</param>
/// <param name="State">The current state id.</param>
/// <param name="Since">When the instance entered the state.</param>
/// <param name="EngineOwned">The badge that replaces the state's glyph, when the engine interrupted the instance.</param>
/// <param name="Subject">The subject, from the recipe's first input.</param>
/// <param name="Inputs">The recipe's inputs, in declaration order.</param>
/// <param name="States">Every state of the recipe, in path order.</param>
/// <param name="Now">The 'next' sentence cut to 80 characters (an ellipsis counts inside the 80).</param>
/// <param name="Actions">The action ids allowed now.</param>
public sealed record InstanceSummary(
    string Address,
    string? ItemId,
    RecipeRef Recipe,
    string State,
    DateTimeOffset Since,
    EngineBadge? EngineOwned,
    SubjectRef? Subject,
    IReadOnlyList<InputSlot> Inputs,
    IReadOnlyList<StateCell> States,
    string Now,
    IReadOnlyList<string> Actions);
