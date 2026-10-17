using Aw.Kernel.Testing;
using Aw.Packets.App.Infrastructure;
using Aw.Packets.Core;
using Aw.Packets.Core.Kinds.Scoping;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.Contracts;

[Lane(TestLane.Integration)]
public sealed class FileSystemScopingPacketSourceContractTests : ScopingPacketSourceContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override string Directory => Path.Combine(_root.Path, "pkt");

    protected override string MissingDirectory => Path.Combine(_root.Path, "missing");

    protected override IScopingPacketSource Create(IReadOnlyDictionary<string, byte[]> files)
    {
        _ = _root.Write("pkt", files);
        return new FileSystemScopingPacketSource();
    }

    [Fact]
    public async Task A_symbolic_link_for_a_listed_file_is_refused_not_followed()
    {
        var files = A.ScopingPacket().Without("brief.md").Build();
        var source = Create(files);
        var outside = Path.Combine(_root.Path, "secret.md");
        await File.WriteAllTextAsync(outside, "outside", TestContext.Current.CancellationToken);
        _ = File.CreateSymbolicLink(Path.Combine(Directory, "brief.md"), outside);

        var read = await source.Read(Directory, CancellationToken.None);

        Assert.Equal(PacketsErrors.Malformed.Code, read.Error.Code);
        Assert.Contains("symbolic link", read.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_linked_entry_that_is_not_listed_is_named_and_not_followed()
    {
        var source = Create(A.ScopingPacket().Build());
        var outside = Path.Combine(_root.Path, "outside");
        _ = System.IO.Directory.CreateDirectory(outside);
        _ = System.IO.Directory.CreateSymbolicLink(Path.Combine(Directory, "extra"), outside);

        var read = await source.Read(Directory, CancellationToken.None);

        Assert.Equal(["extra"], read.Value.Unlisted);
    }

    public void Dispose() => _root.Dispose();
}
