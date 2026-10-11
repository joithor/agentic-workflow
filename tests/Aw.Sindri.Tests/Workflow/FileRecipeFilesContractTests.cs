using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Integration)]
public sealed class FileRecipeFilesContractTests : RecipeFilesContract, IDisposable
{
    private readonly RecipeDirectory _directory = new();

    public void Dispose() => _directory.Dispose();

    protected override IRecipeFiles Files { get; } = new FileRecipeFiles();

    protected override string Put(string name, string text) => _directory.Write(name, text);

    protected override string Missing(string name) => Path.Combine(_directory.Path, "missing", name);
}
