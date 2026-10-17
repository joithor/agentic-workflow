using Aw.Kernel.Testing;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class FileHeavyReserveContractTests : HeavyReserveContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override IHeavyReserve Create() => new FileHeavyReserve(_root.Path);

    [Fact]
    public async Task The_computation_lives_in_the_given_directory_and_no_temporary_file_is_left()
    {
        var reserve = Create();

        _ = await reserve.Write(new HeavyReserveRecord(2, 5, new DateTimeOffset(2026, 10, 15, 5, 0, 0, TimeSpan.Zero)), CancellationToken.None);

        Assert.Equal("2 5 2026-10-15T05:00:00.0000000+00:00", (await File.ReadAllTextAsync(Path.Combine(_root.Path, "heavy-reserve"), TestContext.Current.CancellationToken)).Trim());
        Assert.Equal(["heavy-reserve"], Directory.GetFiles(_root.Path).Select(Path.GetFileName), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("2 5")]
    [InlineData("-2 5 2026-10-15T05:00:00.0000000+00:00")]
    [InlineData("2 5 yesterday")]
    public async Task A_file_that_is_not_a_computation_reads_as_none(string content)
    {
        await File.WriteAllTextAsync(Path.Combine(_root.Path, "heavy-reserve"), content, TestContext.Current.CancellationToken);

        var read = await Create().Read(CancellationToken.None);

        Assert.True(read.IsSuccess);
        Assert.Null(read.Value);
    }

    [Fact]
    public async Task A_directory_that_does_not_exist_yet_is_created_by_the_first_write()
    {
        var reserve = new FileHeavyReserve(Path.Combine(_root.Path, "fresh"));

        var written = await reserve.Write(new HeavyReserveRecord(1, 3, DateTimeOffset.UnixEpoch), CancellationToken.None);

        Assert.True(written.IsSuccess);
        Assert.Equal(1, (await reserve.Read(CancellationToken.None)).Value?.Reserve);
    }

    [Fact]
    public async Task Nothing_is_written_outside_the_given_directory()
    {
        var state = new TempDirectory();
        var team = Path.Combine(state.Path, "meta", "teams", "t1");

        _ = await new FileHeavyReserve(team).Write(new HeavyReserveRecord(1, 3, DateTimeOffset.UnixEpoch), CancellationToken.None);

        Assert.Empty(Directory.GetFiles(state.Path));
        state.Dispose();
    }

    public void Dispose() => _root.Dispose();
}
