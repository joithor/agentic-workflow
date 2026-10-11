namespace Aw.Sindri.Core.Lease;

/// <summary>The grant this process holds, if any: set when the control plane starts, updated on renewal, cleared on stop or loss.</summary>
public sealed class HeldLease
{
    private LeaseGrant? _grant;

    /// <summary>The current grant, or null.</summary>
    public LeaseGrant? Current => Volatile.Read(ref _grant);

    /// <summary>Records <paramref name="grant"/> as held.</summary>
    /// <param name="grant">The grant.</param>
    public void Hold(LeaseGrant grant) => Volatile.Write(ref _grant, grant);

    /// <summary>Forgets the grant (released or lost).</summary>
    public void Clear() => Volatile.Write(ref _grant, null);
}
