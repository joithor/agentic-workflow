using Aw.Sindri.Core.Packets;
using Xunit;

namespace Aw.Sindri.Testing.Packets;

/// <summary>The contract every <see cref="IPacketFiles"/> honours.</summary>
public abstract class PacketFilesContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Packet files holding 'scope-map.json' with the text {"a":1} in the directory <see cref="Directory"/>.</summary>
    /// <returns>The port.</returns>
    protected abstract IPacketFiles Create();

    /// <summary>A directory <see cref="Create"/> holds the file in.</summary>
    protected abstract string Directory { get; }

    /// <summary>A file the packet holds reads as its text.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_file_reads_as_its_text()
    {
        var read = await Create().Read(Directory, "scope-map.json", Token);

        Expect.True(read.IsSuccess);
        Expect.Equal("{\"a\":1}", read.Value);
    }

    /// <summary>A file the packet lacks is an error.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_file_is_an_error() =>
        Expect.True((await Create().Read(Directory, "nothing.json", Token)).IsError);

    /// <summary>A name that is a path is refused.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("../scope-map.json")]
    [InlineData("a/scope-map.json")]
    [InlineData("/etc/passwd")]
    [InlineData(".hidden")]
    [InlineData("")]
    public async Task A_name_that_is_a_path_is_refused(string name) =>
        Expect.True((await Create().Read(Directory, name, Token)).IsError);
}
