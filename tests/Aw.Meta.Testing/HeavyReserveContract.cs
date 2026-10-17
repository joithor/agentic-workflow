using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IHeavyReserve contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class HeavyReserveContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>The adapter under test, with no cap and no reservation set.</summary>
    /// <returns>The adapter.</returns>
    protected abstract IHeavyReserve Create();

    /// <summary>Sets the cap the adapter reads, the way the owner's cap file would.</summary>
    /// <param name="reserve">The adapter returned by <see cref="Create"/>.</param>
    /// <param name="cap">The cap.</param>
    protected abstract void SetCap(IHeavyReserve reserve, int cap);

    /// <summary>With nothing set the cap is the default and nothing is reserved.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Nothing_set_means_the_default_cap_and_no_reservation()
    {
        var read = await Create().Read(_none);

        Expect.That(read.IsSuccess && read.Value.Cap == IHeavyReserve.DefaultCap && read.Value.Reserve == 0, "the defaults are the cap 3 and no reservation");
    }

    /// <summary>A written reservation is read back, and the cap is what was set.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_written_reservation_is_read_back_beside_the_cap()
    {
        var reserve = Create();
        SetCap(reserve, 4);

        var written = await reserve.Write(2, _none);
        var read = await reserve.Read(_none);

        Expect.That(written.IsSuccess && read.IsSuccess && read.Value.Cap == 4 && read.Value.Reserve == 2, "the reservation and the cap are read");
    }

    /// <summary>The reservation is never reported above the cap.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_reservation_above_the_cap_reads_as_the_cap()
    {
        var reserve = Create();
        SetCap(reserve, 2);

        _ = await reserve.Write(5, _none);
        var read = await reserve.Read(_none);

        Expect.That(read.IsSuccess && read.Value.Reserve == 2, "the reservation is clamped to the cap");
    }

    /// <summary>A later write replaces the earlier one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_later_write_replaces_the_earlier_one()
    {
        var reserve = Create();

        _ = await reserve.Write(2, _none);
        _ = await reserve.Write(0, _none);
        var read = await reserve.Read(_none);

        Expect.That(read.IsSuccess && read.Value.Reserve == 0, "the last write wins");
    }
}
