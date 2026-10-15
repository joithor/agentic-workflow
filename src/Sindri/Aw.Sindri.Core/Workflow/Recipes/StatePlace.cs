namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>Where one state sits in the recipe's path order (spec §3.10, UI E-20): a main-path state has an order, a branch state is folded under another.</summary>
/// <param name="State">The state's name.</param>
/// <param name="Order">1, 2, 3 in walk order for a main-path state (terminal states last); null for a branch state.</param>
/// <param name="BranchOf">The state a branch leaves from, or null for a main-path state.</param>
public sealed record StatePlace(string State, int? Order, string? BranchOf);
