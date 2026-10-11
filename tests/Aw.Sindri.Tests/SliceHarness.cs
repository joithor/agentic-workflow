using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests;

/// <summary>The in-memory ports a slice test needs: one store (lease and ledger), profiles, a probe, a clock and the event factory.</summary>
internal sealed class SliceHarness
{
    public SliceHarness()
    {
        Store = new InMemorySindriStore(Clock, Probe);
        Events = new EventFactory(EventScope.ControlPlane, Clock, new SequentialIdGenerator(), Json);
    }

    public static JsonSerializerOptions Json { get; } = AwJson.CreateOptions(SindriJsonContext.Default);

    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    public FakeProcessProbe Probe { get; } = new();

    public InMemorySindriStore Store { get; }

    public InMemoryProfiles Profiles { get; } = new();

    public HeldLease Held { get; } = new();

    public EventFactory Events { get; }

    public FakeShutdown Shutdown { get; } = new();

    /// <summary>Takes the lease for this process and holds it, as StartProcess does.</summary>
    public async Task<LeaseGrant> HoldLease()
    {
        var grant = (await Store.Acquire(Probe.Self(), TestContext.Current.CancellationToken)).Value;
        Held.Hold(grant);
        return grant;
    }

    /// <summary>A checked profile with a heartbeat at <paramref name="every"/>.</summary>
    public static LoadedProfile Profile(string every = "30s", string user = "thor")
    {
        var profile = ProfileRules.Check(new SindriProfile(1, user,
            Services: new Dictionary<string, ServiceSettings>(StringComparer.Ordinal) { ["heartbeat"] = new(every) })).Value;
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, Json)), "/home/profile.yaml");
    }
}
