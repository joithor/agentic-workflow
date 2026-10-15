using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>What the interpreter holds besides the instance (engine spec rev 8 §5.1): the loaded recipe, its blocks' descriptors and the input facts.</summary>
/// <param name="Version">The recipe's id, version and approved hash.</param>
/// <param name="Recipe">Its normalized long form.</param>
/// <param name="Catalog">The build's descriptors of the recipe's blocks.</param>
/// <param name="Inputs">The input facts.</param>
public sealed record InterpretedRecipe(RecipeVersion Version, Recipe Recipe, BlockCatalog Catalog, IReadOnlyDictionary<FactKey, InputFact> Inputs);
