namespace Aw.Sindri.Core.Workflow.Shipped;

/// <summary>
/// The recipes this build ships, embedded in its binary (setup-once spec §5.3): the one place a running Sindri reads <c language="text">recipes/sindri</c> from, so nothing at run time reads that
/// directory from disk. The set is whatever <c language="text">git archive HEAD recipes/sindri</c> staged at publish time; a build that embeds nothing yields an empty set, which is legal.
/// </summary>
public interface IEmbeddedRecipes
{
    /// <summary>Every embedded file, relative to <c language="text">recipes/sindri</c>, sorted by path (ordinal).</summary>
    /// <returns>The files: flat <c language="text">*.yaml</c> recipes and <c language="text">evals/&lt;recipe&gt;/&lt;case&gt;.jsonl</c> cases; empty when the build embeds none.</returns>
    public IReadOnlyList<RecipeSetFile> Files();
}
