using Aw.Kernel.Testing;
using Aw.Sindri.Core.LensPacks;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>LN-WF5: the bundle's files land in the Step directory under the bundle's folder, and none can land elsewhere.</summary>
[Lane(TestLane.Unit)]
public sealed class PackBundleFilesTests : IDisposable
{
    private readonly string _step = Directory.CreateTempSubdirectory("aw-bundle-").FullName;

    public void Dispose() => Directory.Delete(_step, recursive: true);

    private static PackBundle Bundle(params (string Path, string Text)[] files) =>
        new("style", LensPackId.From("plain"), "sha256:" + new string('b', 64), files.ToDictionary(static f => f.Path, static f => f.Text, StringComparer.Ordinal));

    [Fact]
    public void The_files_are_written_under_the_bundles_folder_nested_paths_included()
    {
        var problem = PackBundleFiles.Write(_step, Bundle(("pack.json", "{}"), ("examples/a.md", "An example.")));

        Assert.Null(problem);
        Assert.Equal("{}", File.ReadAllText(Path.Combine(_step, "style", "pack.json")));
        Assert.Equal("An example.", File.ReadAllText(Path.Combine(_step, "style", "examples", "a.md")));
    }

    [Fact]
    public void A_retry_replaces_what_an_earlier_write_left_there()
    {
        _ = PackBundleFiles.Write(_step, Bundle(("old.md", "old")));

        _ = PackBundleFiles.Write(_step, Bundle(("new.md", "new")));

        Assert.False(File.Exists(Path.Combine(_step, "style", "old.md")));
        Assert.True(File.Exists(Path.Combine(_step, "style", "new.md")));
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("a/../../escape.md")]
    [InlineData("/etc/escape.md")]
    public void A_path_that_leaves_the_folder_is_refused_and_nothing_is_written(string path)
    {
        var problem = PackBundleFiles.Write(_step, Bundle(("ok.md", "fine"), (path, "bad")));

        Assert.NotNull(problem);
        Assert.Contains(path, problem.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_step, "style")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_step)!, "escape.md")));   // why: the temp parent is where '../' would land
    }
}
