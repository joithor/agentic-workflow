using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Shipped;

namespace Aw.Sindri.Features.EvalRecipe;

/// <summary>A recipe set in memory as <see cref="IRecipeFiles"/>, so the one-file eval reads a recipe, its child recipes and its eval cases from the embedded set exactly as it reads them from a directory.</summary>
/// <param name="files">The set, relative to recipes/sindri.</param>
/// <param name="root">The virtual directory the set sits in; a path under it is looked up by its relative part.</param>
internal sealed class RecipeSetFiles(IReadOnlyList<RecipeSetFile> files, string root) : IRecipeFiles
{
    public ValueTask<Result<string>> Read(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        var relative = path.StartsWith(root, StringComparison.Ordinal) ? path[root.Length..] : path;
        var file = files.FirstOrDefault(f => string.Equals(f.RelativePath, relative, StringComparison.Ordinal));
        return ValueTask.FromResult(file is null
            ? WorkflowErrors.RecipeUnreadable.With($"{relative} isn't in the embedded recipes (fix: the publish embeds `git archive HEAD recipes/sindri`)")
            : Results.Success(Encoding.UTF8.GetString(file.Bytes.Span)));
    }
}
