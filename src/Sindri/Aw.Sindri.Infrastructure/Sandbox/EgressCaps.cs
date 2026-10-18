namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The per-run caps on the egress proxy, which runs outside the cgroup (sandbox spec §3.2).</summary>
/// <param name="MaxConnections">Concurrent connections.</param>
/// <param name="MaxBytes">Bytes relayed in both directions over the run.</param>
/// <param name="MaxDuration">The longest one connection may stay open.</param>
internal sealed record EgressCaps(int MaxConnections, long MaxBytes, TimeSpan MaxDuration)
{
    /// <summary>The fixed defaults (profile keys later if wanted).</summary>
    public static EgressCaps Default { get; } = new(64, 2L * 1024 * 1024 * 1024, TimeSpan.FromHours(2));
}
