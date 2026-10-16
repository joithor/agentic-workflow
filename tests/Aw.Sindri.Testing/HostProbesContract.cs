using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IHostProbes"/> honours: a path that doesn't exist yet is measured at its nearest existing
/// ancestor, a bound port is reported taken and a free one free, and the fallback is the first free port of the range.
/// Run against the fake and the real adapter (over a temp directory and real loopback listeners).
/// </summary>
public abstract class HostProbesContract
{
    /// <summary>A world with a port that can't be bound, one that can, and a directory that exists.</summary>
    /// <param name="TakenPort">A port nothing can bind.</param>
    /// <param name="FreePort">A port that can be bound.</param>
    /// <param name="Directory">A directory that exists.</param>
    /// <param name="Probes">The probes over that world.</param>
    protected sealed record Scene(int TakenPort, int FreePort, string Directory, IHostProbes Probes);

    /// <summary>Builds a scene; the contract disposes the returned handle when the case ends.</summary>
    /// <param name="holder">What keeps the scene alive (listeners, temp directories).</param>
    /// <returns>The scene.</returns>
    protected abstract Scene Create(out IDisposable holder);

    /// <summary>A path below an existing directory is measured at that directory.</summary>
    [Fact]
    public void A_missing_path_is_measured_at_its_nearest_existing_ancestor()
    {
        var scene = Create(out var holder);
        using var _ = holder;

        var facts = scene.Probes.FreeDisk(Path.Combine(scene.Directory, "absent", "deeper"));

        Expect.Equal(scene.Directory, facts.MeasuredPath);
        Expect.True(facts.FreeBytes > 0);
    }

    /// <summary>A bound port is taken, and the fallback is the first free one.</summary>
    [Fact]
    public void A_taken_preferred_port_names_the_free_fallback()
    {
        var scene = Create(out var holder);
        using var _ = holder;

        var facts = scene.Probes.Ports(scene.TakenPort, scene.FreePort, scene.FreePort);

        Expect.Equal(new PortFacts(scene.TakenPort, PreferredFree: false, scene.FreePort), facts);
    }

    /// <summary>A free preferred port is free; with every fallback taken there is none.</summary>
    [Fact]
    public void A_free_preferred_port_is_free_and_a_taken_range_has_no_fallback()
    {
        var scene = Create(out var holder);
        using var _ = holder;

        var facts = scene.Probes.Ports(scene.FreePort, scene.TakenPort, scene.TakenPort);

        Expect.Equal(new PortFacts(scene.FreePort, PreferredFree: true, FirstFreeFallback: null), facts);
    }
}
