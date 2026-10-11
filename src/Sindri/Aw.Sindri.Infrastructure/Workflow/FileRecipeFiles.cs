using Aw.Kernel;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Recipe files on disk, read as UTF-8 text.</summary>
internal sealed class FileRecipeFiles : IRecipeFiles
{
    public async ValueTask<Result<string>> Read(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            return WorkflowErrors.RecipeUnreadable.With($"{path} doesn't exist (fix: check the path)");
        }

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return WorkflowErrors.RecipeUnreadable.With($"{path} can't be read: {e.Message} (fix: check the path and its permissions)");
        }
    }
}
