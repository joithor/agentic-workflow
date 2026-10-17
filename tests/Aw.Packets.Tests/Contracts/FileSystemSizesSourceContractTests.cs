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

    public void Dispose() => _root.Dispose();

    private sealed class RootedSizesSource(string root, ISizesSource inner) : ISizesSource
    {
        public ValueTask<Aw.Kernel.Result<Aw.Packets.Core.Model.SizesFile>> Read(string path, CancellationToken cancellationToken) =>
            inner.Read(Path.Combine(root, path), cancellationToken);
    }
}
