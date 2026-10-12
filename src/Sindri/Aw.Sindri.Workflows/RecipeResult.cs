namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How a recipe instance ended: the terminal state it reached. P0 contract stub.</summary>
/// <param name="State">The terminal state.</param>
internal sealed record RecipeResult(string State);
