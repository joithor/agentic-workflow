using Aw.Kernel.Testing;
using Aw.Readiness.App.Infrastructure;
using Aw.Readiness.Core.Ports;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The repo-tree contract bound to the file-system adapter on a temp directory (integration lane).</summary>
[Lane(TestLane.Integration)]
public sealed class FileSystemRepoTreeContractTests : RepoTreeContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override IRepoTree TreeWith(IReadOnlyDictionary<string, string> files)
    {
        foreach (var (path, text) in files)
        {
            var full = Path.Combine(_root.Path, path);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);   // why: a joined path always has a directory
            File.WriteAllText(full, text);
        }

        return new FileSystemRepoReader().Open(_root.Path).Value;
    }

    public void Dispose() => _root.Dispose();
}
