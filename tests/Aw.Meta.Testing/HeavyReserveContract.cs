using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IHeavyReserve contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class HeavyReserveContract
{
    private static readonly CancellationToken _none = CancellationToken.None;
    private static readonly DateTimeOffset _at = new(2026, 10, 15, 5, 0, 0, TimeSpan.Zero);

    /// <summary>The adapter under test, with nothing computed yet.</summary>
    /// <returns>The adapter.</returns>
    protected abstract IHeavyReserve Create();

    /// <summary>With nothing written there is no computation.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Nothing_written_reads_as_none()
    {
        var read = await Create().Read(_none);

        Expect.That(read.IsSuccess && read.Value is null, "no computation is kept yet");
    }

    /// <summary>A written computation is read back whole.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_written_computation_is_read_back()
    {
        var reserve = Create();

        var written = await reserve.Write(new HeavyReserveRecord(2, 5, _at), _none);
        var read = await reserve.Read(_none);

        Expect.That(written.IsSuccess && read.IsSuccess && read.Value == new HeavyReserveRecord(2, 5, _at), "the reservation, depth and time are read back");
    }

    /// <summary>A later write replaces the earlier one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_later_write_replaces_the_earlier_one()
    {
        var reserve = Create();

        _ = await reserve.Write(new HeavyReserveRecord(2, 5, _at), _none);
        _ = await reserve.Write(new HeavyReserveRecord(0, 0, _at.AddMinutes(1)), _none);
        var read = await reserve.Read(_none);

        Expect.That(read.IsSuccess && read.Value == new HeavyReserveRecord(0, 0, _at.AddMinutes(1)), "the last write wins");
    }
}
