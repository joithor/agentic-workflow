namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A recipe's states in path order (spec §3.10, UI E-20); derived from the normalized recipe and never hashed.</summary>
/// <param name="OrderedBy">'path', or 'transition-list' when 'initial' has more than one distinct first target.</param>
/// <param name="States">Every declared state, main-path states first by order, then branch states, in walk order.</param>
/// <param name="Loops">The loop edges, in the order the walk found them.</param>
public sealed record PathOrder(string OrderedBy, IReadOnlyList<StatePlace> States, IReadOnlyList<PathLoop> Loops);
