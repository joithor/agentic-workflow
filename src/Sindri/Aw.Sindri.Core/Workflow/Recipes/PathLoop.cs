namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A loop edge of the path walk (spec §3.10): a transition back to a state still on the walk, drawn as the two states side by side.</summary>
/// <param name="Name">The loop's name when one of the two states declares 'loop:' (spec §3.7), else null.</param>
/// <param name="From">The state the back edge leaves.</param>
/// <param name="To">The state it returns to.</param>
public sealed record PathLoop(string? Name, string From, string To);
