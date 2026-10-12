namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>How a recipe instance ended: the terminal state it reached and its outcome. Engine P0 spike.</summary>
/// <param name="State">The terminal state.</param>
/// <param name="Outcome">The terminal outcome ('resolved', or 'cancelled').</param>
internal sealed record RecipeResult(string State, string? Outcome);
