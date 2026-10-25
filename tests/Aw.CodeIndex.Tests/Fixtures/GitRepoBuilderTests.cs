using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Fixtures;

public sealed class GitRepoBuilderTests
{
    [Fact, Trait("Lane", "integration")]
    public void The_tree_holds_a_regular_file_a_symlink_and_a_submodule_entry()
    {
        using var repo = A.GitRepo()
            .WithFile("src/A.cs", "class A {}")
            .WithSymlink("link.cs", "src/A.cs")
            .WithSubmodule("vendor/lib")
            .Build();

        var tree = repo.Git("ls-tree", "-r", "--full-tree", "HEAD");

        Assert.Contains("100644 blob", tree, StringComparison.Ordinal);
        Assert.Contains("120000 blob", tree, StringComparison.Ordinal);
        Assert.Contains("160000 commit", tree, StringComparison.Ordinal);
        Assert.Equal("main", repo.Git("rev-parse", "--abbrev-ref", "HEAD"));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_path_swapped_after_the_commit_is_still_a_file_in_the_tree()
    {
        using var repo = A.GitRepo().WithFile("src/A.cs", "class A {}").Build();

        repo.SwapWithSymlink("src/A.cs", "/etc/hostname");

        Assert.Contains("100644 blob", repo.Git("ls-tree", "HEAD", "src/A.cs"), StringComparison.Ordinal);
        Assert.NotNull(new FileInfo(Path.Combine(repo.Path, "src/A.cs")).LinkTarget);
    }

    [Fact, Trait("Lane", "integration")]
    public void Commit_makes_a_new_head()
    {
        using var repo = A.GitRepo().WithFile("a.txt", "one").Build();
        var first = repo.Head;

        repo.WriteFile("a.txt", "two");
        var second = repo.Commit("second");

        Assert.NotEqual(first, second);
        Assert.Equal(second, repo.Head);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_promisor_clone_has_the_tree_but_not_the_blobs()
    {
        using var clone = A.GitRepo().WithFile("src/A.cs", "class A {}").BuildPromisorClone();

        var blob = clone.Git("ls-tree", "HEAD", "src/A.cs").Split('\t')[0].Split(' ')[2];

        Assert.NotNull(clone.Origin);
        Assert.Equal("true", clone.Git("config", "remote.origin.promisor"));
        _ = Assert.Throws<InvalidOperationException>(() => clone.Git("-c", "protocol.allow=never", "cat-file", "-e", blob));
    }
}
