namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The input of one recipe instance (engine spec rev 8 §5.1). P0 contract stub: the workflow id is the instance address;
/// P4 adds the recipe's (id, version, hash), the attempt, the input facts and the tenant and ring keys.
/// </summary>
/// <param name="Address">The instance address, '{item}/{recipe-name}/{attempt}'.</param>
internal sealed record RecipeRun(string Address);
