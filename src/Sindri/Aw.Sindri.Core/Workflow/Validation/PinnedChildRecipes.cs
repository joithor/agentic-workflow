using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The child recipes an instance's approval pinned (engine P7 plan D2, workflow engine spec §4.1, §5.8): the approval's
/// resolved children, each handed out only at the hash the pins name. A child that is loaded under another hash, or isn't
/// pinned, doesn't resolve, so the child's creation refuses it (SND-RECIPE-002) and a replay sees the same answer.
/// </summary>
/// <param name="loaded">The approval's resolved child recipes, with their grandchildren, by id.</param>
/// <param name="pins">The hash the approval pinned for each.</param>
public sealed class PinnedChildRecipes(IReadOnlyDictionary<RecipeId, ChildRecipe> loaded, IReadOnlyDictionary<RecipeId, RecipeHash> pins) : IChildRecipeResolver
{
    /// <inheritdoc/>
    public ChildRecipe? Resolve(RecipeId id)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(pins);
        return loaded.TryGetValue(id, out var child) && pins.TryGetValue(id, out var pin) && pin == child.Hash ? child : null;
    }
}
