using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// U1's freshness (UI spec U10, §4.1, §3.2): the pill from the status's health (E-14) and its asOf, the stalled and
/// rebuilding banners from the services view, the catalog read on connect, and every view re-read on an event.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class FreshnessTests : IDisposable
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly HealthViewModel _vm;
    private Task _run = Task.CompletedTask;

    public FreshnessTests()
    {
        _records.Write(A.Record());
        _vm = new HealthViewModel(_records, _server, _clock);
    }

    public void Dispose()
    {
        _stop.Cancel();
        Assert.True(_run.Wait(TimeSpan.FromSeconds(2), CancellationToken.None), "Run ended when cancelled");
        _stop.Dispose();
    }

    private async Task Connected(Func<bool>? also = null)
    {
        _run = _vm.Run(_stop.Token);
        for (var i = 0; i < 200 && !(_vm.State == ShellState.Normal && (also?.Invoke() ?? true)); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(ShellState.Normal, _vm.State);
    }

    private static StatusView Status(AsOfView views, HealthView health) => A.Status() with { ApiVersion = 1, Views = views, Health = health };

    // E-14: the pill renders health.word and names each problem as served; "updated Ns ago" counts from projectedAt.
    [Fact]
    public async Task The_pill_says_the_health_word_its_problems_and_how_long_since_the_views_moved()
    {
        _server.Current = Status(new AsOfView(41, 41, A.Noon.AddSeconds(-12), Rebuilding: false, Stalled: false),
            new HealthView("degraded", [new HealthProblemView("SND-SCHED-001", "index failed")]));

        await Connected();

        Assert.Equal(("Degraded", "updated 12s ago", "index failed"), (_vm.Pill.Word, _vm.Pill.Age, _vm.Pill.Problems));
        Assert.Equal("Degraded · updated 12s ago · index failed", _vm.Pill.Text);
        Assert.Equal((StateGlyph.Down, false), (_vm.Pill.Glyph, _vm.Pill.IsHealthy));
    }

    [Fact]
    public async Task Healthy_is_the_ok_glyph_and_no_problems()
    {
        _server.Current = Status(new AsOfView(41, 41, A.Noon, Rebuilding: false, Stalled: false), new HealthView("healthy", []));

        await Connected();

        Assert.Equal(("Healthy · updated 0s ago", StateGlyph.Ok, true), (_vm.Pill.Text, _vm.Pill.Glyph, _vm.Pill.IsHealthy));
    }

    // Before API P2 the status has no health or views: U0's pill, its age from the last event or ping.
    [Fact]
    public async Task Without_health_the_pill_says_running_as_before()
    {
        _server.ServiceView = new RouteAnswer(HttpStatusCode.NotFound, "{}");

        await Connected();

        Assert.Equal(("Running · updated 0s ago", false), (_vm.Pill.Text, _vm.Banner.IsShown));
    }

    // §4.1 Stalled: the view's own sentence and Rebuild views, the catalog's row (ready once P2 ships it).
    [Fact]
    public async Task Stalled_views_show_the_views_sentence_and_rebuild_views()
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");

        await Connected(() => _vm.Banner.IsShown);

        Assert.Equal((true, false, "Views are stalled at 42: run `sindri views rebuild`"), (_vm.Banner.IsStalled, _vm.Banner.IsRebuilding, _vm.Banner.Text));
        Assert.Equal(("RebuildViews", true), (_vm.Banner.Rebuild?.Action, _vm.Banner.Rebuild?.CanRun));
    }

    // A catalog that still has RebuildViews pending (P1's, before P2): the banner's control is disabled with its focusable line.
    [Fact]
    public async Task With_rebuild_still_pending_the_banners_control_is_drawn_pending()
    {
        var p2 = RouteAnswer.Api("commands", "catalog");
        _server.Catalog = p2 with { Body = p2.Body.Replace("\"cli\": \"sindri views rebuild\",\n      \"ui\": \"ready\"", "\"cli\": \"sindri views rebuild\",\n      \"ui\": \"pending:P2\"", StringComparison.Ordinal) };
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");

        await Connected(() => _vm.Banner.IsShown);

        Assert.Equal((false, "arrives in P2 · sindri views rebuild"), (_vm.Banner.Rebuild?.CanRun, _vm.Banner.Rebuild?.PendingLine));
    }

    [Fact]
    public async Task Rebuilding_views_show_a_progress_banner_without_rebuild()
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "rebuilding");

        await Connected(() => _vm.Banner.IsShown);

        Assert.Equal((false, true, "Rebuilding views (cursor 12 of 42)…", false), (_vm.Banner.IsStalled, _vm.Banner.IsRebuilding, _vm.Banner.Text, _vm.Banner.HasRebuild));
    }

    // §9.1: the catalog is read on connect; Health lists its pending rows by phase (§4.8).
    [Fact]
    public async Task The_catalog_is_read_on_connect_and_its_pending_rows_are_grouped_by_phase()
    {
        await Connected(() => _vm.Catalog.Controls.Count > 0);

        Assert.Equal(1, _server.CatalogCalls);
        Assert.Equal(37, _vm.Catalog.Controls.Count);
        Assert.Equal("27 actions arrive in later phases", _vm.Catalog.PendingSummary);
        Assert.Equal(["arrives in WF-P4", "arrives in WF-P5", "arrives in P6", "arrives in P3", "arrives in P4", "arrives in P10a", "arrives in S-5B"], _vm.Catalog.Pending.Select(p => p.Heading), StringComparer.Ordinal);
        Assert.Equal(6, _vm.Catalog.CliOnly.Count);
    }

    // §9.2: an event re-reads every view the app shows (the status and the services view); its payload is never applied.
    [Fact]
    public async Task An_event_re_reads_the_status_and_the_services_view()
    {
        await Connected(() => _server.ServicesCalls > 0);
        var (status, services) = (_server.StatusCalls, _server.ServicesCalls);
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");

        _server.Push(A.Event(1, "views.stalled"));
        for (var i = 0; i < 200 && !_vm.Banner.IsShown; i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(_vm.Banner.IsStalled);
        Assert.True(_server.StatusCalls > status && _server.ServicesCalls > services);
    }
    // §9.2: a burst re-reads each view at most once per 250 ms. The first event re-reads at once; the events in the next
    // 250 ms collapse into one more re-read when the window ends.
    [Fact]
    public async Task A_burst_of_events_re_reads_each_view_once_per_250_ms()
    {
        await Connected(() => _server.ServicesCalls > 0);
        var (status, services) = (_server.StatusCalls, _server.ServicesCalls);

        _server.Push(A.Event(1, "service.ran"));
        await Until(() => _server.ServicesCalls == services + 1);
        _server.Push(A.Event(2, "service.ran"), A.Event(3, "service.ran"), A.Event(4, "service.ran"));
        await Task.Delay(150, TestContext.Current.CancellationToken);

        Assert.Equal((status + 1, services + 1), (_server.StatusCalls, _server.ServicesCalls));

        _clock.Advance(HealthViewModel.RefreshEvery);
        await Until(() => _server.ServicesCalls == services + 2);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal((status + 2, services + 2), (_server.StatusCalls, _server.ServicesCalls));
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
