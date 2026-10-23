using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Core.Setup;

/// <summary>Counts the registered recipes for the setup list.</summary>
public static class SetupRecipes
{
    /// <summary>
    /// Each recipe id once, at its highest registered version (the one that would run); a recipe whose revoked rows are all it has can't be approved and isn't counted.
    /// Approved means that version is approved at its current hash and the approval isn't stale (a stale approval needs approving again).
    /// </summary>
    /// <param name="lines">Every registered recipe line, by id and version.</param>
    /// <returns>How many can be approved and how many of those are.</returns>
    public static RecipesFact Count(IReadOnlyList<RecipeSummary> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var current = lines.GroupBy(static l => l.Id)
            .Select(static g => g.Where(static l => l.Standing != RecipeStanding.Revoked).ToList())
            .Where(static rows => rows.Count > 0)
            .Select(static rows => rows.Where(l => l.Version == rows.Max(static r => r.Version)).ToList())
            .ToList();

        // Several rows at the highest version are an older hash and the shipped one: the recipe is approved only when every one is.
        return new RecipesFact(current.Count, current.Count(static rows => rows.TrueForAll(static l => l is { Standing: RecipeStanding.Approved, Stale.Count: 0 })));
    }
}
