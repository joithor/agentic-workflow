using Aw.Kernel.Testing;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class FileSystemSizesSourceContractTests : SizesSourceContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override ISizesSource Create(string path, byte[] bytes)
    {
        // the contract reads by the path it was created with, relative to the working directory: use the absolute path via a rooted source
        var full = Path.Combine(_root.Path, path);
        File.WriteAllBytes(full, bytes);
        return new RootedSizesSource(_root.Path, new FileSystemSizesSource(PacketFiles.Json));
    }

    [Fact]
    public async Task A_directory_is_not_a_sizes_file()
    {
        var source = Create("sizes.json", []);
        _ = Directory.CreateDirectory(Path.Combine(_root.Path, "folder"));

        var read = await source.Read("folder", TestContext.Current.CancellationToken);

        Assert.Equal(PacketsErrors.SizesUnusable.Code, read.Error.Code);
    }

    public void Dispose() => _root.Dispose();

    private sealed class RootedSizesSource(string root, ISizesSource inner) : ISizesSource
    {
        public ValueTask<Kernel.Result<Core.Model.SizesFile>> Read(string path, CancellationToken cancellationToken) =>
            inner.Read(Path.Combine(root, path), cancellationToken);
    }
}
