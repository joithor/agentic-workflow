using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>A directory on disk, read as a recipe set: its flat <c language="text">*.yaml</c> files and <c language="text">evals/&lt;recipe&gt;/*.jsonl</c>.</summary>
internal sealed class FileRecipeDirectories : IRecipeDirectories
{
    public ValueTask<Result<IReadOnlyList<RecipeSetFile>>> Read(string directory, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not built yet.");
}
