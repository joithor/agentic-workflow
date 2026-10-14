using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>A registered recipe (spec §4.1): the normalized text kept by its hash, with the key it is approved under.</summary>
/// <param name="Id">The recipe's id.</param>
/// <param name="Version">Its version.</param>
/// <param name="Hash">The hash of its normalized long form.</param>
/// <param name="Text">The normalized text the hash covers.</param>
/// <param name="Base">The base a generated recipe was made from, or null for a hand-written one.</param>
/// <param name="RegisteredAt">When it was first registered.</param>
public sealed record RegisteredRecipe(RecipeId Id, int Version, RecipeHash Hash, string Text, RecipeRef? Base, DateTimeOffset RegisteredAt);
