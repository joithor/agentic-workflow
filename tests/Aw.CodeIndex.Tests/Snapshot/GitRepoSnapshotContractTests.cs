using Aw.CodeIndex.Infrastructure.Snapshot;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Snapshot;

[Trait("Lane", "integration")]
public sealed class GitRepoSnapshotContractTests : RepoSnapshotContract
{
    protected override ArrangedSnapshot Arrange(RepoLayout layout)
    {
        var builder = A.GitRepo();
        foreach (var (path, text) in layout.Files)
        {
            _ = builder.WithFile(path, text);
        }

        foreach (var path in layout.Symlinks)
        {
            _ = builder.WithSymlink(path, "target");
        }

        foreach (var path in layout.Submodules)
        {
            _ = builder.WithSubmodule(path);
        }

        var repo = layout.BlobsMissing ? builder.BuildPromisorClone() : builder.Build();
        var commit = layout.UnsafeDirectory is { } directory ? repo.CommitUnsafeNames(directory, [.. layout.UnsafeNames]) : repo.Head;
        return new ArrangedSnapshot(new GitRepoSnapshot(), repo.AsRepo(), commit, repo);
    }
}
