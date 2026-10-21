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
    /// <summary>A world with a port that can't be bound, one that can, a range of taken ports, and a directory that exists.</summary>
    /// <param name="TakenPort">A port nothing can bind.</param>
    /// <param name="FreePort">A port that can be bound once <paramref name="ReleaseFreePort"/> has run (until then the scene holds it; after it, nothing guarantees the port stays free, so cases retry).</param>
    /// <param name="Directory">A directory that exists.</param>
    /// <param name="Probes">The probes over that world.</param>
    /// <param name="TakenRangeFirst">The first port of a range whose every port is taken.</param>
    /// <param name="TakenRangeLast">The last port of that range, inclusive.</param>
    /// <param name="ReleaseFreePort">Releases <paramref name="FreePort"/> just before it is probed (safe to call twice).</param>
    protected sealed record Scene(int TakenPort, int FreePort, string Directory, IHostProbes Probes, int TakenRangeFirst, int TakenRangeLast, Action ReleaseFreePort);

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

    /// <summary>An existing directory is measured as itself, and a missing one is not walked up from.</summary>
    [Fact]
    public void FreeDiskIfExists_measures_an_existing_directory_and_is_null_for_a_missing_one()
    {
        var scene = Create(out var holder);
        using var _ = holder;

        var present = scene.Probes.FreeDiskIfExists(scene.Directory);
        var absent = scene.Probes.FreeDiskIfExists(Path.Combine(scene.Directory, "absent"));

        Expect.Equal(scene.Directory, present?.MeasuredPath);
        Expect.True(present?.FreeBytes > 0);
        Expect.True(absent is null);
    }

    /// <summary>A bound port is taken, and the fallback is the first free one.</summary>
    [Fact]
    public void A_taken_preferred_port_names_the_free_fallback()
    {
        PortFacts expected = default!;
        PortFacts actual = default!;
        Expect.True(AnyAttemptMatches(scene =>
        {
            actual = scene.Probes.Ports(scene.TakenPort, scene.FreePort, scene.FreePort);
            expected = new PortFacts(scene.TakenPort, PreferredFree: false, scene.FreePort);
            return expected == actual;
        }), $"expected {expected}, got {actual}");
    }

    /// <summary>A free preferred port is free.</summary>
    [Fact]
    public void A_free_preferred_port_is_free()
    {
        PortFacts expected = default!;
        PortFacts actual = default!;
        Expect.True(AnyAttemptMatches(scene =>
        {
            actual = scene.Probes.Ports(scene.FreePort, scene.TakenRangeFirst, scene.TakenRangeLast);
            expected = new PortFacts(scene.FreePort, PreferredFree: true, FirstFreeFallback: null);
            return expected == actual;
        }), $"expected {expected}, got {actual}");
    }

    private const int FreePortAttempts = 5;

    // A released port is only free until something else takes it: another process, or (Linux loopback) the probe's own connect
    // picking the destination port as its ephemeral source port and self-connecting, which reads as taken. So a case that
    // expects the released port free rebuilds its scene and retries up to FreePortAttempts times and passes when any attempt
    // matches. The ceiling stays 5: a probe that wrongly reads taken half the time would still pass about 97% of runs, which
    // is the price of not flaking; a probe that always reads taken fails every attempt. The fake matches on the first attempt.
    private bool AnyAttemptMatches(Func<Scene, bool> probe)
    {
        for (var attempt = 0; attempt < FreePortAttempts; attempt++)
        {
            var scene = Create(out var holder);
            using (holder)
            {
                scene.ReleaseFreePort();
                if (probe(scene))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>A taken preferred port with every port of the fallback range taken has no fallback: the range itself is probed.</summary>
    [Fact]
    public void A_taken_preferred_port_with_every_fallback_taken_has_no_fallback()
    {
        var scene = Create(out var holder);
        using var _ = holder;

        var facts = scene.Probes.Ports(scene.TakenPort, scene.TakenRangeFirst, scene.TakenRangeLast);

        Expect.True(scene.TakenRangeLast > scene.TakenRangeFirst);
        Expect.Equal(new PortFacts(scene.TakenPort, PreferredFree: false, FirstFreeFallback: null), facts);
    }
}
