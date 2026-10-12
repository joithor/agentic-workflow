using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The input of one recipe instance (engine spec rev 8 §5.1). Engine P0 spike: the workflow id is the instance address
/// '{item}/{recipe-name}/{attempt}', and the recipe is 'workflow.{recipe-name}' from the address; P4 adds the recipe's
/// (id, version, hash) and the tenant and ring keys.
/// </summary>
/// <param name="Address">The instance address, '{item}/{recipe-name}/{attempt}'.</param>
/// <param name="Inputs">The input facts by fact key ('fact:spec'), as stored references.</param>
internal sealed record RecipeRun(string Address, IReadOnlyDictionary<string, FactValue> Inputs);
