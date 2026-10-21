using Aw.Kernel.Testing;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Packets;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Packets;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Packets;

/// <summary>The disk packet files honour the packet-files contract, and refuse a directory outside Sindri's home.</summary>
[Lane(TestLane.Integration)]
public sealed class DiskPacketFilesContractTests : PacketFilesContract, IDisposable
{
    private readonly TempHome _home = new();

    /// <inheritdoc/>
    protected override string Directory => Path.Combine(_home.Path, "workflow", "runs", "WFI-1", "scope-packet");

    public void Dispose() => _home.Dispose();

    /// <inheritdoc/>
    protected override IPacketFiles Create()
    {
        _ = System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(Path.Combine(Directory, "scope-map.json"), "{\"a\":1}");
        return Files();
    }

    /// <summary>A directory outside Sindri's home is refused, even for a file that exists.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_directory_outside_the_home_is_refused()
    {
        using var other = new TempHome();
        await File.WriteAllTextAsync(Path.Combine(other.Path, "scope-map.json"), "{}", TestContext.Current.CancellationToken);

        Expect.True((await Files().Read(other.Path, "scope-map.json", TestContext.Current.CancellationToken)).IsError);
        Expect.True((await Files().Read("relative/dir", "scope-map.json", TestContext.Current.CancellationToken)).IsError);
    }

    private DiskPacketFiles Files() => new(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")));
}
