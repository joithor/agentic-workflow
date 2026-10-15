using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>X-Sindri-After is best effort (API §2.4, UI spec §9.1): the shell shows what Sindri says as soon as it can.</summary>
public sealed partial class FreshnessTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    // G1(a): a rebuild is accepted and the projection is stalled, so the server answers SND-API-011 at once. The sequence
    // is used up by that one answer: the re-read goes without it, and the stalled banner shows within the same beat.
    [Fact]
    public async Task A_stalled_projection_answers_views_behind_once_and_the_re_read_shows_it_stalled()
    {
        await Connected(() => _server.ServicesCalls > 0);
        _server.Lag = ViewsLag.Stalled;
        _ = await _server.RebuildViews(_api, TestContext.Current.CancellationToken);
        _server.Current = Status(new AsOfView(42, 42, A.Noon, Rebuilding: false, Stalled: true), new HealthView("degraded", []));
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");

        _server.Push(A.Event(1, "views.rebuild.started"));
        await Until(() => _vm.Banner.IsStalled);

        Assert.True(_vm.Banner.IsStalled);
        Assert.Equal(("Stalled", ShellState.Normal), (_vm.Pill.Word, _vm.State));
        Assert.Equal(1, _server.StatusAfters.Count(a => a > 0));
    }

    // G1(b): the projection is behind (rebuilding): the server waits 2 s, then SND-API-011. The next read has no header
    // and shows the rebuilding banner.
    [Fact]
    public async Task A_projection_behind_answers_views_behind_once_and_the_re_read_shows_it_rebuilding()
    {
        await Connected(() => _server.ServicesCalls > 0);
        _server.Lag = ViewsLag.Behind;
        _ = await _server.RebuildViews(_api, TestContext.Current.CancellationToken);
        _server.Current = Status(new AsOfView(12, 42, A.Noon, Rebuilding: true, Stalled: false), new HealthView("healthy", []));
        _server.ServiceView = RouteAnswer.SpecShaped("services", "rebuilding");

        _server.Push(A.Event(1, "views.rebuild.started"));
        await Until(() => _vm.Banner.IsRebuilding);

        Assert.True(_vm.Banner.IsRebuilding);
        Assert.Equal(1, _server.StatusAfters.Count(a => a > 0));
    }

    // G2: the first status on connect answers SND-API-011 (a sequence left over from before): it is read again once at
    // once and shown, so the shell is Normal without waiting for an event.
    [Fact]
    public async Task A_views_behind_answer_on_connect_is_read_again_and_shown()
    {
        _ = await _server.RebuildViews(_api, TestContext.Current.CancellationToken);
        _server.Lag = ViewsLag.Behind;

        await Connected();

        Assert.Equal(ShellState.Normal, _vm.State);
        Assert.Equal([43L, 0L], _server.StatusAfters.Take(2));
    }
}
