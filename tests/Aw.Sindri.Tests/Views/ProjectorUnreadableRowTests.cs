using System.Globalization;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.Views;

/// <summary>A stored row an older build wrote that the current shape can't read is absent to the fold, never a stall (#959): the projector rebuilds the views from the ledger.</summary>
public sealed class ProjectorUnreadableRowTests
{
    private readonly SliceHarness _h = new();
    private readonly InMemoryViewStore _views = new(SliceHarness.Json);
    private readonly ListLogger<Projector> _log = new();
    private readonly StaleShapeProjection _projection = new();
    private readonly ProjectorProgress _progress;

    public ProjectorUnreadableRowTests()
    {
        _progress = new ProjectorProgress(_h.Clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_fold_that_reads_an_unreadable_row_does_not_stall_and_the_views_rebuild_from_the_ledger()
    {
        await Append(2);

        var pass = await Projector().CatchUp(Token);

        Assert.Equal(ProjectorPass.CaughtUp, pass);
        Assert.Equal(4, _projection.Calls);   // the two events, then the replay of both
        Assert.True(_progress.Current is { Cursor: 2, Stalled: false, Rebuilding: false });
        Assert.Null((await _views.Get<JobRow>(ViewNames.Profile, "stale", Token)).Value);   // the replay emptied the view
        Assert.Equal(2, (await _views.List<ServiceRow>(ViewNames.Services, Token)).Value.Count);
        Assert.Contains(_log.Lines, l => l.Contains("unreadable", StringComparison.OrdinalIgnoreCase));
    }

    private Projector Projector() => new(new CountingLedgerReader(_h.Store), _views, [_projection], _progress, _h.Callers, _h.Clock, _log);

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
