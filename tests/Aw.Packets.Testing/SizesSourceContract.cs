using System.Text;
using Aw.Packets.Core;
using Xunit;

namespace Aw.Packets.Testing;

/// <summary>The ISizesSource contract, run against the in-memory fake and the disk adapter.</summary>
public abstract class SizesSourceContract
{
    /// <summary>A good file reads with its limits.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_sizes_file_reads()
    {
        var read = await Create("sizes.json", SizesBuilder.Bytes(A.Sizes())).Read("sizes.json", CancellationToken.None);

        Expect.That(read.IsSuccess && read.Value.Sizes.Medium.Files == 20 && read.Value.Sizes.Small.Budget.Turns == 60, "the limits are as written");
    }

    /// <summary>A missing file is AW-PACKETS-009.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_missing_file_is_AW_PACKETS_009()
    {
        var read = await Create("sizes.json", SizesBuilder.Bytes(A.Sizes())).Read("elsewhere.json", CancellationToken.None);

        Expect.That(read.IsError && read.Error.Code == PacketsErrors.SizesUnusable.Code, "a missing file is AW-PACKETS-009");
    }

    /// <summary>Each way a file can be wrong is AW-PACKETS-009.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("not json")]
    [InlineData("{\"schemaVersion\": 2, \"sizes\": {}}")]
    [InlineData("{\"schemaVersion\": 1, \"sizes\": {\"XS\": {\"files\": 1, \"items\": 1, \"budget\": {}}}}")]
    [InlineData("{\"schemaVersion\": 1, \"sizes\": {}, \"extra\": 1}")]
    public async Task A_bad_file_is_AW_PACKETS_009(string text)
    {
        var read = await Create("sizes.json", Encoding.UTF8.GetBytes(text)).Read("sizes.json", CancellationToken.None);

        Expect.That(read.IsError && read.Error.Code == PacketsErrors.SizesUnusable.Code, "a bad file is AW-PACKETS-009");
    }

    /// <summary>A negative limit is AW-PACKETS-009.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_negative_limit_is_AW_PACKETS_009()
    {
        var text = Encoding.UTF8.GetString(SizesBuilder.Bytes(A.Sizes())).Replace("\"files\": 8", "\"files\": -1", StringComparison.Ordinal);

        var read = await Create("sizes.json", Encoding.UTF8.GetBytes(text)).Read("sizes.json", CancellationToken.None);

        Expect.That(read.IsError && read.Error.Code == PacketsErrors.SizesUnusable.Code, "a negative limit is AW-PACKETS-009");
    }

    /// <summary>Creates a source holding one file.</summary>
    /// <param name="path">Its path.</param>
    /// <param name="bytes">Its bytes.</param>
    /// <returns>The source under test.</returns>
    protected abstract ISizesSource Create(string path, byte[] bytes);
}
