using Aw.Kernel;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>Recipe files on disk. Red stub (P1 unit (a)).</summary>
internal sealed class FileRecipeFiles : IRecipeFiles
{
    public ValueTask<Result<string>> Read(string path, CancellationToken cancellationToken) => throw new NotSupportedException("red: recipe files land with P1 unit (a)");
}
