using System.Globalization;
using Aw.Kernel.Testing;
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

    protected override void SetCap(IHeavyReserve reserve, int cap) =>
        File.WriteAllText(Path.Combine(_root.Path, "heavy-slots-cap"), cap.ToString(CultureInfo.InvariantCulture) + "\n");

    [Fact]
    public async Task The_wrapper_reads_the_file_as_a_bare_number_and_no_temporary_file_is_left()
    {
        var reserve = Create();

        _ = await reserve.Write(2, CancellationToken.None);

        Assert.Equal("2", (await File.ReadAllTextAsync(Path.Combine(_root.Path, "heavy-meta-reserve"), TestContext.Current.CancellationToken)).Trim());
        Assert.Equal(["heavy-meta-reserve"], Directory.GetFiles(_root.Path).Select(Path.GetFileName), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-2")]
    public async Task A_cap_file_that_is_not_a_positive_number_means_the_default(string content)
    {
        await File.WriteAllTextAsync(Path.Combine(_root.Path, "heavy-slots-cap"), content, TestContext.Current.CancellationToken);

        var read = await Create().Read(CancellationToken.None);

        Assert.Equal(IHeavyReserve.DefaultCap, read.Value.Cap);
    }

    [Fact]
    public async Task A_state_root_that_does_not_exist_yet_is_created_by_the_first_write()
    {
        var reserve = new FileHeavyReserve(Path.Combine(_root.Path, "fresh"));

        var written = await reserve.Write(1, CancellationToken.None);

        Assert.True(written.IsSuccess);
        Assert.Equal(1, (await reserve.Read(CancellationToken.None)).Value.Reserve);
    }

    public void Dispose() => _root.Dispose();
}
