using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Snapshot;

[Trait("Lane", "unit")]
public sealed class FakeRepoSnapshotContractTests : RepoSnapshotContract
{
    private static readonly CommitSha _commit = CommitSha.From("0123456789abcdef0123456789abcdef01234567");

    protected override ArrangedSnapshot Arrange(RepoLayout layout)
    {
        var snapshot = new FakeRepoSnapshot();
        foreach (var (path, text) in layout.Files)
        {
            _ = snapshot.WithFile(_commit, path, text);
            if (layout.BlobsMissing)
            {
                _ = snapshot.WithMissingBlob(_commit, path);
            }
        }

        foreach (var path in layout.Symlinks)
        {
            _ = snapshot.WithSkipped(_commit, path, SkipReason.Symlink);
        }

        foreach (var path in layout.Submodules)
        {
            _ = snapshot.WithSkipped(_commit, path, SkipReason.Submodule);
        }

        foreach (var name in layout.UnsafeNames)
        {
            _ = snapshot.WithFile(_commit, $"{layout.UnsafeDirectory}/{name}", "unsafe");
        }

        return new ArrangedSnapshot(snapshot, new CodeIndexRepo(RepoName.From("fixture"), "/nonexistent", CodeIndexSettings.Default), _commit, Cleanup: null);
    }
}
