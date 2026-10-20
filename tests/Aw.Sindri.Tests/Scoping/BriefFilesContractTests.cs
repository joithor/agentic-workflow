using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Infrastructure.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The disk brief reader honours the brief-file contract over a temp directory, and refuses a link that leaves its root.</summary>
[Lane(TestLane.Integration)]
public sealed class BriefFilesContractTests : BriefFilesContract, IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "aw-brief-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }

    protected override IBriefFiles Create(IReadOnlyDictionary<string, string> files, out string root)
    {
        root = Path.Combine(_base, "repo");
        _ = Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(_base, "a.md"), "outside");   // what "../a.md" would reach
        foreach (var (path, text) in files)
        {
            var full = Path.Combine(root, path);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(full)!);   // why: every path has a directory part under the root
            File.WriteAllText(full, text);
        }

        return new BriefFiles();
    }

    [Fact]
    public async Task A_symbolic_link_that_leaves_the_root_is_refused()
    {
        var reader = Create(new Dictionary<string, string>(StringComparer.Ordinal), out var root);
        var outside = Path.Combine(_base, "elsewhere");
        _ = Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "brief.md"), "outside", TestContext.Current.CancellationToken);
        _ = Directory.CreateSymbolicLink(Path.Combine(root, "link"), outside);

        var read = await reader.Read("link/brief.md", [root], TestContext.Current.CancellationToken);

        Assert.Equal(ScopingErrors.BriefPathRefused.Code, read.Error.Code);
    }

    [Fact]
    public async Task A_circular_symbolic_link_is_refused_and_the_other_files_still_read()
    {
        var reader = Create(new Dictionary<string, string>(StringComparer.Ordinal) { ["ok.md"] = "fine" }, out var root);
        _ = File.CreateSymbolicLink(Path.Combine(root, "a.md"), Path.Combine(root, "b.md"));
        _ = File.CreateSymbolicLink(Path.Combine(root, "b.md"), Path.Combine(root, "a.md"));
        _ = File.CreateSymbolicLink(Path.Combine(root, "self.md"), Path.Combine(root, "self.md"));

        var loop = await reader.Read("a.md", [root], TestContext.Current.CancellationToken);
        var self = await reader.Read("self.md", [root], TestContext.Current.CancellationToken);
        var ok = await reader.Read("ok.md", [root], TestContext.Current.CancellationToken);

        Assert.Equal(ScopingErrors.BriefPathRefused.Code, loop.Error.Code);
        Assert.Equal(ScopingErrors.BriefPathRefused.Code, self.Error.Code);
        Assert.Equal("fine", ok.Value.Text);
    }
}
