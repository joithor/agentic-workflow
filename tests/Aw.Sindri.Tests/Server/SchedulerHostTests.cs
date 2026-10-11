using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Host.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.Server;

public sealed class SchedulerHostTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task The_scheduler_runs_the_heartbeat_on_the_approved_cadence()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);
        _ = await _h.HoldLease();
        await using var provider = Services();
        using var scheduler = new SchedulerHost(provider.GetRequiredService<IServiceScopeFactory>(), _h.Profiles, _h.Held, _h.Clock, NullLogger<SchedulerHost>.Instance);

        await scheduler.StartAsync(Token);
        await Until(() => Heartbeats() == 1);
        for (var i = 0; i < 3; i++)
        {
            _h.Clock.Advance(TimeSpan.FromSeconds(5));
            await Until(() => Heartbeats() == i + 2);
        }

        await scheduler.StopAsync(Token);
        Assert.Equal(4, Heartbeats());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Nothing_runs_without_an_approved_profile_or_without_the_lease()
    {
        await using var provider = Services();
        using var scheduler = new SchedulerHost(provider.GetRequiredService<IServiceScopeFactory>(), _h.Profiles, _h.Held, _h.Clock, NullLogger<SchedulerHost>.Instance);
        await scheduler.StartAsync(Token);
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);   // approved, but no lease held

        _h.Clock.Advance(TimeSpan.FromSeconds(11));
        await Task.Delay(300, Token);

        await scheduler.StopAsync(Token);
        Assert.Equal(0, Heartbeats());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_lost_lease_clears_the_grant_and_stops_the_process()
    {
        var grant = await _h.HoldLease();
        _ = await _h.Store.Release(grant, Token);
        var shutdown = new FakeShutdown();
        using var renewer = new LeaseRenewer(_h.Store, _h.Held, shutdown, _h.Clock, NullLogger<LeaseRenewer>.Instance);

        await renewer.StartAsync(Token);
        await Task.Delay(100, Token);
        _h.Clock.Advance(LeaseTiming.RenewEvery);
        await Until(() => shutdown.Requested);

        await renewer.StopAsync(Token);
        Assert.Null(_h.Held.Current);
        Environment.ExitCode = 0;
    }

    private int Heartbeats() => _h.Store.Events.Count(e => e.Envelope.Type == EventTypes.Heartbeat);

    private ServiceProvider Services() => new ServiceCollection()
        .AddSingleton<ILedgerStore>(_h.Store)
        .AddSingleton(_h.Events)
        .AddSingleton<TimeProvider>(_h.Clock)
        .AddScoped<IService<RecordHeartbeatRequest, RecordHeartbeatResponse>, RecordHeartbeatService>()
        .AddScoped<IScheduledService, HeartbeatService>()
        .BuildServiceProvider();

    private static async Task Until(Func<bool> condition)
    {
        for (var waited = 0; !condition(); waited += 20)
        {
            Assert.True(waited < 5000, "the condition didn't hold within 5 s");
            await Task.Delay(20, Token);
        }
    }
}
