using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.Sessions;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The collector host on the fake clock (collector spec CO2, CI-4): it runs only with the lease, ledgers 'appeared' after two passes and 'gone' when the process ends.</summary>
public sealed class SessionCollectorHostTests
{
    private readonly CollectorHarness _c = new();
    private readonly ListLogger<SessionCollectorHost> _log = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ServiceProvider Services() => new ServiceCollection()
        .AddSingleton<ILedgerStore>(_c.Base.Store)
        .AddSingleton<ILedgerReader>(_c.Base.Store)
        .AddSingleton(_c.Base.Events)
        .AddSingleton(_c.Memory)
        .AddSingleton<IService<CollectSessionsRequest, CollectSessionsResponse>>(_c.Service)
        .AddScoped<IService<RecordFleetChangesRequest, RecordFleetChangesResponse>, RecordFleetChangesService>()
        .AddScoped<IService<RestoreFleetHistoryRequest, RestoreFleetHistoryResponse>>(sp => new RestoreFleetHistoryService(_c.Base.Store, _c.Base.Events, _c.Memory, _c.Base.Clock))
        .BuildServiceProvider();

    private async Task Run(Action<int>? between = null, int steps = 0)
    {
        await using var provider = Services();
        var clock = new SteppedClock(_c.Base.Clock);
        using var host = new SessionCollectorHost(provider.GetRequiredService<IServiceScopeFactory>(), _c.Base.Held, _c.Base.Callers, _c.Settings, _c.Snapshot, clock, _log);
        await host.StartAsync(Token);
        await clock.Armed();
        for (var i = 0; i < steps; i++)
        {
            between?.Invoke(i);
            await clock.Step(TimeSpan.FromSeconds(2));
            await clock.Armed();
        }

        await host.StopAsync(Token);
    }

    private List<(string Change, string Id)> Changes() => [.. _c.Base.Store.Events.Where(static e => e.Envelope.Type == EventTypes.FleetChanged)
        .Select(e => _c.Base.Events.Payload<FleetChanged>(e).Value).Select(static p => (p.Change.ToString(), p.SessionId.Value))];

    [Fact, Trait("Lane", "unit")]
    public async Task The_host_ledgers_appeared_after_two_passes_and_gone_when_the_process_ends()
    {
        _ = await _c.Base.HoldLease();
        _ = _c.Fake.With(Proc(10, 1, "claude", argv0: "claude", ticks: 100));

        await Run(between: i => { if (i == 3) { _c.Fake.Exit(10); } }, steps: 5);

        Assert.Equal([("Appeared", "pid-10-100"), ("Gone", "pid-10-100")], Changes());
        Assert.All(_c.Base.Store.Events.Where(static e => e.Envelope.Type == EventTypes.FleetChanged), e => Assert.Equal("service:session-collector", e.Envelope.Actor));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_is_collected_without_the_lease()
    {
        _ = _c.Fake.With(Proc(10, 1, "claude", argv0: "claude"));

        await Run(steps: 3);

        Assert.Empty(Changes());
        Assert.Null(_c.Snapshot.Current);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_restarted_host_does_not_ledger_appeared_again_for_a_session_it_already_reported()
    {
        _ = await _c.Base.HoldLease();
        _ = _c.Fake.With(Proc(10, 1, "claude", argv0: "claude", ticks: 100));
        await Run(steps: 3);
        var first = Changes();

        _c.Memory.Tracker = FleetTrackerState.Empty;   // a new process starts with nothing in memory
        _c.Memory.Restored = false;
        await Run(steps: 3);

        Assert.Equal([("Appeared", "pid-10-100")], first);
        Assert.Equal(first, Changes());
    }
}
