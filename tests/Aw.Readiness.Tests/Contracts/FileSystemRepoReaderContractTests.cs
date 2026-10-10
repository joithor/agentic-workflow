using Aw.Kernel.Testing;
using Aw.Readiness.App.Infrastructure;
using Aw.Readiness.Core.Ports;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The repo-reader contract bound to the file-system adapter on a temp directory (integration lane).</summary>
[Lane(TestLane.Integration)]
public sealed class FileSystemRepoReaderContractTests : RepoReaderContract, IDisposable
{
    private readonly TempDirectory _root = new();

    public FileSystemRepoReaderContractTests()
    {
        File.WriteAllText(Path.Combine(_root.Path, "README.md"), "x");
    }

    protected override string ExistingRoot => _root.Path;

    protected override string FileRoot => Path.Combine(_root.Path, "README.md");

    protected override string MissingRoot => Path.Combine(_root.Path, "missing");

    protected override IRepoReader NewReader() => new FileSystemRepoReader();

    public void Dispose() => _root.Dispose();
}
