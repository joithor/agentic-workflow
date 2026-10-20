namespace Aw.Sindri.Core.Workflow.Shipped;

/// <summary>Builds a <see cref="RecipeManifest"/> from a recipe set (setup-once spec §5.3), with the registry's own hash function: the reader's.</summary>
public static class RecipeManifests
{
    /// <summary>The manifest of <paramref name="files"/>.</summary>
    /// <param name="files">The recipe set: flat <c language="text">*.yaml</c> files and <c language="text">evals/&lt;recipe&gt;/*.jsonl</c>.</param>
    /// <param name="reader">The recipe reader; its hash is the registry's.</param>
    /// <returns>The manifest.</returns>
    public static RecipeManifest Build(IReadOnlyList<RecipeSetFile> files, IRecipeReader reader) =>
        throw new NotSupportedException("Not built yet.");
}
