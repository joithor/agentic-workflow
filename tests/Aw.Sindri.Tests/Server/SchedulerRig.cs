using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.RecordServiceRun;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>A scheduler on the stepped fake clock with the real pause view and guard (API P6 unit D): a test pauses, resumes and steps seconds, and reads what was recorded.</summary>
internal sealed class SchedulerRig : IAsyncDisposable
{
    private readonly ServiceProvider _provider;
    private readonly SteppedClock _clock;
    private readonly SchedulerHost _scheduler;
    private readonly InMemoryViewStore _views = new(SliceHarness.Json);
    private long _sequence;

    private SchedulerRig(IEnumerable<IScheduledService> services)
    {
        var collection = new ServiceCollection()
            .AddSingleton<ILedgerStore>(H.Store)
            .AddSingleton<ILedgerReader>(H.Store)
            .AddSingleton(H.Events)
            .AddSingleton<TimeProvider>(H.Clock)
            .AddSingleton(H.Callers)
            .AddSingleton<IIdGenerator>(H.Ids)
            .AddRecordServiceRun();
        foreach (var service in services)
        {
            _ = collection.AddSingleton(service);
        }

        _provider = collection.BuildServiceProvider();
        _clock = new SteppedClock(H.Clock);
        _scheduler = new SchedulerHost(_provider.GetRequiredService<IServiceScopeFactory>(), new ScheduleInputs(H.Profiles, [], new ViewServicePauses(_views)), Guard, H.Held, H.Callers, _clock,
            new ListLogger<SchedulerHost>());
    }

    public SliceHarness H { get; } = new();

    public ServiceRunGuard Guard { get; } = new();

    /// <summary>Approves a profile naming <paramref name="every"/> for each service, takes the lease and leaves the scheduler to <see cref="Begin"/>.</summary>
    /// <param name="services">The services this build has.</param>
    /// <param name="every">The services the profile names, each with its cadence.</param>
    /// <returns>The running rig.</returns>
    public static async Task<SchedulerRig> Start(IEnumerable<IScheduledService> services, params (string Name, string Every)[] every)
    {
        var rig = new SchedulerRig(services);
        _ = await rig.H.Profiles.Save(SliceHarness.ProfileOf("thor", every), TestContext.Current.CancellationToken);
        _ = await rig.H.HoldLease();
        return rig;
    }

    /// <summary>Starts the scheduler (its first slot is due at once).</summary>
    /// <returns>A task that completes then.</returns>
    public Task Begin() => _scheduler.StartAsync(TestContext.Current.CancellationToken);

    /// <summary>Advances the fake clock <paramref name="seconds"/> one second at a time, letting the loop finish each step.</summary>
    /// <param name="seconds">How many.</param>
    /// <returns>A task that completes then.</returns>
    public async Task Step(int seconds)
    {
        for (var n = 0; n < seconds; n++)
        {
            await _clock.Step(TimeSpan.FromSeconds(1));
            await _clock.Armed();
        }
    }

    public Task Pause(string service) => Fold(EventTypes.ServicePaused, new ServicePaused(H.Ids.New<CommandId>(), ServiceName.From(service)));

    public Task Resume(string service) => Fold(EventTypes.ServiceResumed, new ServiceResumed(H.Ids.New<CommandId>(), ServiceName.From(service)));

    /// <summary>The recorded runs of <paramref name="service"/>, in order.</summary>
    /// <param name="service">The service.</param>
    /// <returns>The runs.</returns>
    public IReadOnlyList<ServiceRan> Runs(string service) => [.. H.Store.Events
        .Where(e => string.Equals(e.Envelope.Type, EventTypes.ServiceRan, StringComparison.Ordinal))
        .Select(e => H.Events.Payload<ServiceRan>(e).Value)
        .Where(r => string.Equals(r.Service.Value, service, StringComparison.Ordinal))];

    public async ValueTask DisposeAsync()
    {
        await _scheduler.StopAsync(TestContext.Current.CancellationToken);
        _scheduler.Dispose();
        await _provider.DisposeAsync();
    }

    private async Task Fold<TPayload>(string type, TPayload payload)
    {
        var ct = TestContext.Current.CancellationToken;
        _ = (await _views.Open(ViewSchema.Version, ct)).Value;
        var applied = (await _views.Apply([Recorded.Of(H.Events, type, payload, ++_sequence)], [new ServicesProjection(H.Events)], H.Clock.GetUtcNow(), ct)).Value;
        Assert.Null(applied.Stall);
    }
}
