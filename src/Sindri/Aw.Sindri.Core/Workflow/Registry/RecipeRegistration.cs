namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>What registering a recipe did: the stored row, and whether this call added it (a second register of the same hash adds nothing).</summary>
/// <param name="Recipe">The stored recipe.</param>
/// <param name="Added">True when this call stored it; false when the hash was already registered.</param>
public sealed record RecipeRegistration(RegisteredRecipe Recipe, bool Added);
