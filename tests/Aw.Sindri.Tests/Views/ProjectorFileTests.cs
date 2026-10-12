using System.Globalization;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The projector on real files: views.db is derived, so deleting it and starting again rebuilds the same rows.</summary>
public sealed class ProjectorFileTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly SequentialIdGenerator _ids = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Deleting_views_db_and_restarting_rebuilds_the_same_views()
    {
        await Append(5);
        var before = await ProjectOnce();

        foreach (var file in Directory.GetFiles(_home.Path, "views.db*"))
        {
            File.Delete(file);
        }

        var after = await ProjectOnce();

        Assert.Equal(5, before.Count);
        Assert.Equal(before, after);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_projector_host_catches_up_in_the_background_and_follows_the_head()
    {
        await Append(2);
        using var views = new SqliteViews(_home.Path);
        using var ledger = Ledger();
        var progress = new ProjectorProgress(_clock);
        var projector = new Projector(new SqliteLedgerReader(ledger), views.Store, [new ToggleProjection()], progress, new AsyncLocalCallerContextAccessor(_ids),
            _clock, new ListLogger<Projector>());
        using var host = new ProjectorHost(projector, _clock, new ListLogger<ProjectorHost>());

        await host.StartAsync(Token);
        await Until(() => progress.Current.Cursor == 2);
        await Append(1);
        await Until(() => progress.Current.Cursor == 3, tick: true);
        await host.StopAsync(Token);

        Assert.Equal(3, (await views.Reader.List<ServiceRow>(ViewNames.Services, Token)).Value.Count);
    }

    private async Task<IReadOnlyList<ServiceRow>> ProjectOnce()
    {
        using var views = new SqliteViews(_home.Path);
        using var ledger = Ledger();
        var projector = new Projector(new SqliteLedgerReader(ledger), views.Store, [new ToggleProjection()], new ProjectorProgress(_clock),
            new AsyncLocalCallerContextAccessor(_ids), _clock, new ListLogger<Projector>());
        Assert.Equal(ProjectorPass.CaughtUp, await projector.CatchUp(Token));
        return (await views.Reader.List<ServiceRow>(ViewNames.Services, Token)).Value;
    }

    private LedgerDatabase Ledger() => new(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _clock);

    private async Task Append(int count)
    {
        var probe = new FakeProcessProbe();
        var stores = SqliteStores.Create(_home.Path, _clock, probe);
        var grant = (await stores.Lease.Acquire(probe.Self(), Token)).Value;
        var head = (await stores.Ledger.Head(Token)).Value;
        for (var i = 1; i <= count; i++)
        {
            var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId()).Value;
            _ = await stores.Ledger.Append(new LedgerEntry(context.Envelope(ToggleProjection.Put, """{"n":1}""", _clock, _ids), string.Create(CultureInfo.InvariantCulture, $"k{head + i:D4}"), Item: null), grant, Token);
        }

        _ = await stores.Lease.Release(grant, Token);
    }

    /// <summary>Waits (2 s of real time at most) for <paramref name="done"/>; with <paramref name="tick"/> it moves the fake clock a second each try, past the loop's wait.</summary>
    private async Task Until(Func<bool> done, bool tick = false)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            if (tick)
            {
                _clock.Advance(TimeSpan.FromSeconds(1));
            }

            await Task.Delay(10, Token);
        }

        Assert.True(done());
    }
}
