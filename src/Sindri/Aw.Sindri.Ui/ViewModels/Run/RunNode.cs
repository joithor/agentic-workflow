namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>One card on the canvas.</summary>
/// <param name="Id">The state id on the line.</param>
/// <param name="Kind">Stage, side or end.</param>
/// <param name="Name">The card's name in words.</param>
/// <param name="Order">The state's path order, or null off the main path.</param>
/// <param name="BranchOf">The state a branch leaves, or null.</param>
/// <param name="State">Where the card is.</param>
/// <param name="Outcome">An end's outcome, or null.</param>
/// <param name="WorkflowName">The display name of the workflow the stage runs, or null.</param>
/// <param name="ChildId">The instance id of the child the card shows (the first one not done), or null.</param>
/// <param name="ChildAddress">That child's address, or null.</param>
/// <param name="Steps">The step rows (stages only).</param>
/// <param name="EnteredAt">When the latest visit to the state began; null when the line does not say.</param>
/// <param name="LeftAt">When the latest visit ended; null while it is current or the line does not say.</param>
/// <param name="StateKind">Who does the state, as its recipe says; null when the recipe serves none.</param>
internal sealed record RunNode(
    string Id,
    RunNodeKind Kind,
    string Name,
    int? Order,
    string? BranchOf,
    RunNodeState State,
    string? Outcome,
    string? WorkflowName,
    string? ChildId,
    string? ChildAddress,
    IReadOnlyList<RunStep> Steps,
    DateTimeOffset? EnteredAt = null,
    DateTimeOffset? LeftAt = null,
    Api.Workflow.RecipeStateKind? StateKind = null);
