using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow.Shipped;

/// <summary>Reads a directory laid out like <c language="text">recipes/sindri</c> as a recipe set (for <c language="text">recipe manifest &lt;dir&gt;</c>).</summary>
public interface IRecipeDirectories
{
    /// <summary>The recipe files of <paramref name="directory"/>: its flat <c language="text">*.yaml</c> files and <c language="text">evals/&lt;recipe&gt;/*.jsonl</c>, sorted by path (ordinal).</summary>
    /// <param name="directory">The directory, as the user gave it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The files, or SND-RECIPE-010 when the directory doesn't exist or can't be read.</returns>
    public ValueTask<Result<IReadOnlyList<RecipeSetFile>>> Read(string directory, CancellationToken cancellationToken);
}
