namespace Aw.Sindri.Core.Workflow.Stats;

/// <summary>The key of a row in the 'recipe-stats' view.</summary>
public static class RecipeStatsKey
{
    /// <summary>'&lt;recipe&gt;/&lt;window&gt;/&lt;hash or -&gt;'.</summary>
    /// <param name="recipe">The recipe id.</param>
    /// <param name="hash">The hash as asked, or null for every version.</param>
    /// <param name="window">The window's text.</param>
    /// <returns>The key.</returns>
    public static string Of(string recipe, string? hash, string window)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(window);
        return $"{recipe}/{window}/{hash ?? "-"}";
    }
}
