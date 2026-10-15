using System.Globalization;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.Views;

/// <summary>A projector that can't run shows it in the status and the health (SND-VIEW-001), and a rebuild request survives a failed attempt.</summary>
public sealed class ProjectorFailureTests
{
    private readonly SliceHarness _h = new();
    private readonly InMemoryViewStore _memory = new(SliceHarness.Json);
    private readonly FlakyViewStore _views;
    private readonly ProjectorProgress _progress;
    private readonly ToggleProjection _projection = new();

    public ProjectorFailureTests()
    {
        _views = new FlakyViewStore(_memory);
        _progress = new ProjectorProgress(_h.Clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_store_that_cannot_open_shows_in_the_status_and_the_health_until_a_pass_works()
    {
        await Append(2);
        _views.FailOpens = 1;

        var pass = await Projector().CatchUp(Token);

        Assert.Equal(ProjectorPass.Failed, pass);
        var shown = _progress.Current;
        Assert.True(shown is { Stalled: true, LedgerHead: 2 });
        Assert.Contains("SND-VIEW-001", shown.StalledType, StringComparison.Ordinal);
        var health = HealthSummary.For(running: true, shown, [], profileNeedsApproval: false, profileChanged: false);
        Assert.NotEqual(HealthWord.Healthy, health.Word);
        Assert.Contains(health.Problems, p => p.Code == "SND-VIEW-001");

        Assert.Equal(ProjectorPass.CaughtUp, await Projector().CatchUp(Token));
        Assert.True(_progress.Current is { Stalled: false, StalledType: null, Cursor: 2 });
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_request_survives_a_failed_open_and_the_next_pass_rebuilds()
    {
        var projector = Projector();
        await Append(3);
        _ = await projector.CatchUp(Token);
        var calls = _projection.Calls;
        _progress.RequestRebuild();
        _views.FailOpens = 1;

        Assert.Equal(ProjectorPass.Failed, await projector.CatchUp(Token));
        Assert.Equal(ProjectorPass.CaughtUp, await projector.CatchUp(Token));

        Assert.Equal(calls + 3, _projection.Calls);   // the request wasn't lost: the views replayed
        Assert.True(_progress.Current is { Cursor: 3, Rebuilding: false });
    }

    private Projector Projector() => new(new CountingLedgerReader(_h.Store), _views, [_projection], _progress, _h.Callers, _h.Clock, new ListLogger<Projector>());

    private async Task Append(int count)
    {
        var grant = (await _h.Store.Read(Token)).Value is null ? await _h.HoldLease() : _h.Held.Current!;   // why: Read found the held lease
        for (var i = 0; i < count; i++)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"k{_h.Store.Events.Count + 1:D4}");
            var context = EventContext.Create("host:sindri", "local", 0, _h.Ids.NewId()).Value;
            _ = await _h.Store.Append(new LedgerEntry(context.Envelope(ToggleProjection.Put, """{"n":1}""", _h.Clock, _h.Ids), key, Item: null), grant, Token);
        }
    }
}
