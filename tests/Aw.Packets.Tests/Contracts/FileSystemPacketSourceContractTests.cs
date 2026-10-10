using Aw.Kernel.Testing;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class FileSystemPacketSourceContractTests : PacketSourceContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override string Directory => Path.Combine(_root.Path, "pkt");

    protected override string MissingDirectory => Path.Combine(_root.Path, "missing");

    protected override IPacketSource Create(IReadOnlyDictionary<string, byte[]> files)
    {
        _ = _root.Write("pkt", files);
        return new FileSystemPacketSource(PacketFiles.Json);
    }

    [Fact]
    public async Task A_symbolic_link_is_refused_not_followed()
    {
        var files = PacketFiles.Of(A.Packet().Build());
        _ = files.Remove("design/design.md");
        var source = Create(files);
        var outside = Path.Combine(_root.Path, "secret.md");
        await File.WriteAllTextAsync(outside, "outside", TestContext.Current.CancellationToken);
        _ = System.IO.Directory.CreateDirectory(Path.Combine(Directory, "design"));
        _ = File.CreateSymbolicLink(Path.Combine(Directory, "design", "design.md"), outside);

        var loaded = await source.Load(Directory, CancellationToken.None);

        Assert.Equal(PacketsErrors.Malformed.Code, loaded.Error.Code);
        Assert.Contains("symbolic link", loaded.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_trailing_slash_on_the_directory_loads()
    {
        var source = Create(PacketFiles.Of(A.Packet().Build()));

        var loaded = await source.Load(Directory + "/", CancellationToken.None);

        Assert.True(loaded.IsSuccess, loaded.IsError ? loaded.Error.Message : null);
    }

    [Fact]
    public async Task A_symbolic_link_to_a_directory_is_refused_not_followed()
    {
        var files = PacketFiles.Of(A.Packet().Build());
        _ = files.Remove("design/design.md");
        var source = Create(files);
        var outside = Path.Combine(_root.Path, "outside");
        _ = System.IO.Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "design.md"), "outside", TestContext.Current.CancellationToken);
        _ = System.IO.Directory.CreateSymbolicLink(Path.Combine(Directory, "design"), outside);

        var loaded = await source.Load(Directory, CancellationToken.None);

        Assert.Equal(PacketsErrors.Malformed.Code, loaded.Error.Code);
        Assert.Contains("symbolic link", loaded.Error.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _root.Dispose();
}
