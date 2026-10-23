using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Setup;

/// <summary>Counts the registered recipes for the setup list.</summary>
public static class SetupRecipes
{
    /// <summary>
    /// Each recipe id once, at its highest registered version (the one that would run); a recipe whose highest version is revoked can't be approved and isn't counted.
    /// Approved means that version is approved at its current hash.
    /// </summary>
    /// <param name="lines">Every registered recipe line, by id and version.</param>
    /// <returns>How many can be approved and how many of those are.</returns>
    public static RecipesFact Count(IReadOnlyList<RecipeSummary> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var current = lines.GroupBy(static l => l.Id).Select(static g => g.MaxBy(static l => l.Version)).OfType<RecipeSummary>()
            .Where(static l => l.Standing != RecipeStanding.Revoked)
            .ToList();
        return new RecipesFact(current.Count, current.Count(static l => l.Standing == RecipeStanding.Approved));
    }
}
