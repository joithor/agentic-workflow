using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>A directory on disk, read as a recipe set: its flat <c language="text">*.yaml</c> files and <c language="text">evals/&lt;recipe&gt;/*.jsonl</c>; anything else in it is left alone.</summary>
internal sealed class FileRecipeDirectories : IRecipeDirectories
{
    public async ValueTask<Result<IReadOnlyList<RecipeSetFile>>> Read(string directory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!Directory.Exists(directory))
        {
            return WorkflowErrors.RecipeUnreadable.With($"{directory} isn't a directory (fix: name the directory that holds the recipe .yaml files)");
        }

        try
        {
            var files = new List<RecipeSetFile>();
            foreach (var path in Paths(directory))
            {
                files.Add(new RecipeSetFile(path, await File.ReadAllBytesAsync(Path.Combine(directory, path), cancellationToken).ConfigureAwait(false)));
            }

            return files;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return WorkflowErrors.RecipeUnreadable.With($"{directory} can't be read: {e.Message} (fix: check the path and its permissions)");
        }
    }

    /// <summary>The relative paths: top-level *.yaml, and *.jsonl one level below each directory of evals/; sorted (ordinal).</summary>
    private static List<string> Paths(string directory)
    {
        var paths = Directory.EnumerateFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).OfType<string>().ToList();
        var evals = Path.Combine(directory, "evals");
        if (Directory.Exists(evals))
        {
            paths.AddRange(Directory.EnumerateDirectories(evals).SelectMany(
                recipe => Directory.EnumerateFiles(recipe, "*.jsonl", SearchOption.TopDirectoryOnly).Select(f => $"evals/{Path.GetFileName(recipe)}/{Path.GetFileName(f)}")));
        }

        paths.Sort(StringComparer.Ordinal);
        return paths;
    }
}
