using Aw.Kernel.Testing;
using Aw.Readiness.App.Infrastructure;
using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.Tests.Contracts;

/// <summary>The file-system tree never reads or lists through a symbolic link, at any depth (integration lane).</summary>
[Lane(TestLane.Integration)]
public sealed class FileSystemRepoTreeLinkTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private (IRepoTree Tree, string Outside) Checkout()
    {
        var root = Directory.CreateDirectory(Path.Combine(_dir.Path, "repo")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(_dir.Path, "outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "secret.txt"), "outside");
        File.WriteAllText(Path.Combine(outside, "Leak.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(root, "README.md"), "inside");
        return (new FileSystemRepoReader().Open(root).Value, outside);
    }

    [Fact]
    public void A_file_under_a_linked_directory_reads_as_missing()
    {
        var (tree, outside) = Checkout();
        _ = Directory.CreateSymbolicLink(Path.Combine(_dir.Path, "repo", "docs"), outside);

        Assert.Null(tree.ReadText("docs/secret.txt"));
        Assert.False(tree.Exists("docs/secret.txt"));
        Assert.Equal("inside", tree.ReadText("README.md"));
    }

    [Fact]
    public void A_linked_start_directory_lists_nothing()
    {
        var (tree, outside) = Checkout();
        _ = Directory.CreateSymbolicLink(Path.Combine(_dir.Path, "repo", "src"), outside);

        Assert.Empty(tree.Files("src", ".csproj"));
    }

    [Fact]
    public void A_linked_file_reads_as_missing()
    {
        var (tree, outside) = Checkout();
        _ = File.CreateSymbolicLink(Path.Combine(_dir.Path, "repo", "AGENTS.md"), Path.Combine(outside, "secret.txt"));

        Assert.Null(tree.ReadText("AGENTS.md"));
    }
}
