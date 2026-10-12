using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What the 'LoadRecipe' local activity hands the interpreter (engine spec rev 8 §5.1): the normalized recipe with its
/// hash, and the descriptors of the blocks it uses, so every decision reads them from history and stays deterministic.
/// </summary>
/// <param name="Recipe">The recipe's long form, its <see cref="Recipe.Hash"/> set.</param>
/// <param name="Blocks">The build's descriptors of the blocks the recipe names.</param>
internal sealed record LoadedRecipe(Recipe Recipe, IReadOnlyList<BlockDescriptor> Blocks);
