namespace Aw.Kernel.Testing;

/// <summary>
/// The clock a contract base hands to the implementation it checks: starts at <see cref="KnownInstants.Epoch"/> and
/// moves only when the contract advances it. Aw.Kernel.Testing keeps to one external dependency, so this is a
/// minimal <see cref="TimeProvider"/> rather than 'FakeTimeProvider'; implementations only read
/// <see cref="TimeProvider.GetUtcNow"/> from it.
/// </summary>
internal sealed class ContractClock : TimeProvider
{
    private DateTimeOffset _now = KnownInstants.Epoch;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
