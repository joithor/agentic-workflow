using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

[Lane(TestLane.Unit)]
public sealed class HealthViewModelTests : IDisposable
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly HealthViewModel _vm;

    public HealthViewModelTests()
    {
        _vm = new HealthViewModel(_records, _server, _clock);
    }

    private Task _run = Task.CompletedTask;

    public void Dispose()
    {
        _stop.Cancel();
        Assert.True(_run.Wait(TimeSpan.FromSeconds(2), CancellationToken.None), "Run ended when cancelled");
        _stop.Dispose();
    }

    private void Start() => _run = _vm.Run(_stop.Token);

    private static async Task Eventually(Func<bool> condition, string what)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), what);
    }

    [Fact]
    public async Task No_endpoint_record_reads_as_not_running_with_the_sindri_fix()
    {
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Equal("sindri", _vm.Fix);
        Assert.Contains("memory:control.json", _vm.Reason, StringComparison.Ordinal);
        Assert.False(_vm.IsRunning);
    }

    [Fact]
    public async Task A_record_for_another_process_reads_as_stale()
    {
        _records.Write(A.Record(pid: 1, epoch: 7));
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Contains("stale", _vm.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_record_nothing_answers_at_reads_as_not_running()
    {
        _records.Write(A.Record());
        _server.GoDown();
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Contains("127.0.0.1:7410", _vm.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Running_shows_the_lease_profile_and_services_as_the_cli_does()
    {
        _records.Write(A.Record());
        Start();

        await Eventually(() => _vm.Headline == "Running", "headline");
        Assert.True(_vm.IsRunning);
        Assert.Equal("pid 4242 · epoch 7", _vm.Lease);
        Assert.Equal("approved", _vm.ProfileState);
        Assert.Equal("hash ab12cd · /home/profile.yaml", _vm.ProfileDetail);
        Assert.Equal("", _vm.ProfileFix);
        Assert.Equal([new ServiceRow("heartbeat", "every 5s", "last run 12:00:00Z")], _vm.Services);
        Assert.Equal("127.0.0.1:7410", _vm.Api);
    }

    [Fact]
    public async Task The_health_line_words_the_status_in_4_8s_order()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.ChangedProfile(), services: [A.Heartbeat(), A.Heartbeat()]) with { ApiVersion = 1 };
        Start();

        await Eventually(() => _vm.Headline == "Running", "headline");
        Assert.Equal("Running · pid 4242 · epoch 7 · API v1 · 2 services · profile changed since approval", _vm.HealthLine);
    }

    [Fact]
    public async Task The_health_line_leaves_out_what_the_status_does_not_carry()
    {
        _records.Write(A.Record());
        Start();

        await Eventually(() => _vm.Headline == "Running", "headline");
        Assert.Equal("Running · pid 4242 · epoch 7 · 1 service · profile approved", _vm.HealthLine);
    }

    [Fact]
    public async Task Not_running_is_the_whole_health_line()
    {
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Equal("Not running", _vm.HealthLine);
    }

    [Fact]
    public async Task A_profile_changed_since_approval_names_the_approve_fix()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.ChangedProfile());
        Start();

        await Eventually(() => _vm.ProfileState == "changed since approval", "profile state");
        Assert.Equal("running with approved ab12cd; /home/profile.yaml is now ff00ee", _vm.ProfileDetail);
        Assert.Equal("sindri profile approve", _vm.ProfileFix);
    }

    [Fact]
    public async Task An_unapproved_profile_names_the_approve_fix()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.Unapproved());
        Start();

        await Eventually(() => _vm.ProfileState == "not approved", "profile state");
        Assert.Equal("sindri profile approve", _vm.ProfileFix);
    }

    [Fact]
    public async Task The_approve_fix_follows_sindris_verdict_not_the_hashes()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.Approved() with { NeedsApproval = true });
        Start();

        await Eventually(() => _vm.Headline == "Running", "running");
        Assert.Equal("sindri profile approve", _vm.ProfileFix);
        Assert.True(_vm.HasProfileFix);
    }

    [Fact]
    public async Task An_event_refreshes_the_status_and_shows_as_the_last_event()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");

        _server.Current = A.Status(services: A.Heartbeat(A.Noon.AddSeconds(5)));
        _server.Push(A.Event(12, at: A.Noon.AddSeconds(5)));

        await Eventually(() => _vm.Services is [{ LastRun: "last run 12:00:05Z" }], "heartbeat last run");
        Assert.Equal("service.heartbeat · 12:00:05Z · #12", _vm.LastEvent);
    }

    [Fact]
    public async Task A_dropped_stream_goes_to_not_running_and_reconnects_from_its_cursor()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        _server.Push(A.Event(30));
        await Eventually(() => _vm.LastEvent.EndsWith("#30", StringComparison.Ordinal), "event 30");

        _records.Remove();
        _server.GoDown();
        await Eventually(() => _vm.Headline == "Not running", "down");

        _server.ComeUp();
        _server.Pong = new(4242, 8);
        _server.Current = A.Status(epoch: 8);
        _records.Write(A.Record(epoch: 8));
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Lease == "pid 4242 · epoch 8";
        }, "reconnected");
        Assert.Equal(30, _server.StreamCursors[^1]);
    }

    [Fact]
    public async Task A_restart_is_followed_while_the_old_stream_still_hangs_open()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");

        // The old control plane is stopping but holds its event stream open; a new one records another endpoint.
        _server.Pong = new(5151, 8);
        _server.Current = A.Status(pid: 5151, epoch: 8);
        _records.Write(A.Record(pid: 5151, epoch: 8, port: 7493));

        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Lease == "pid 5151 · epoch 8";
        }, "followed the new endpoint");
        Assert.Equal("127.0.0.1:7493", _vm.Api);
    }

    [Fact]
    public async Task A_failed_refresh_ends_the_stream_instead_of_waiting_on_it()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        var streams = _server.StreamCursors.Count;

        _server.Current = A.Status(epoch: 7);
        _server.FailStatus = true;
        _server.Push(A.Event(40, "process.stopped"));
        await Eventually(() => _vm.Headline == "Not running", "status failed");
        _server.FailStatus = false;

        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _server.StreamCursors.Count > streams && _vm.Headline == "Running";
        }, "reopened the stream");
    }

    [Fact]
    public async Task An_unreadable_endpoint_record_reads_as_not_running_with_the_reason()
    {
        _records.WriteText("{not json");
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Contains("can't be read", _vm.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_record_with_a_relative_url_reads_as_not_running()
    {
        _records.WriteText("""{"url":"sindri/","pid":4242,"epoch":7}""");
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Contains("can't be read", _vm.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_status_without_the_lease_reads_as_not_running()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { Running = false };
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Contains("holds no lease", _vm.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_record_the_platform_cant_read_reads_as_not_running_and_the_app_keeps_trying()
    {
        _records.Write(A.Record());
        _records.FailNextRead(new UnauthorizedAccessException("Access to the path is denied."));
        Start();

        await Eventually(() => _vm.Headline == "Not running", "not running");
        Assert.Contains("Access to the path is denied.", _vm.Reason, StringComparison.Ordinal);
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Headline == "Running";
        }, "recovered on the next try");
    }

    [Fact]
    public async Task A_new_ledger_after_a_reconnect_restarts_the_stream_from_the_start()
    {
        _records.Write(A.Record(epoch: 7));
        _server.Push(A.Event(30));
        Start();
        await Eventually(() => _vm.LastEvent.EndsWith("#30", StringComparison.Ordinal), "event 30");

        _records.Write(A.Record(epoch: 1));
        _server.Pong = new(4242, 1);
        _server.Current = A.Status(epoch: 1);
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Lease == "pid 4242 · epoch 1";
        }, "reconnected to the new ledger");
        Assert.Equal(0, _server.StreamCursors[^1]);
    }

    [Fact]
    public async Task An_event_stream_that_ends_at_once_waits_before_reconnecting()
    {
        _records.Write(A.Record());
        _server.StreamsEndAtOnce = true;
        Start();
        await Eventually(() => _server.StreamCursors.Count == 1, "first stream opened");

        await Task.Delay(200, TestContext.Current.CancellationToken);
        _ = Assert.Single(_server.StreamCursors);

        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _server.StreamCursors.Count >= 2;
        }, "reconnected once the wait ended");
    }

    [Fact]
    public async Task A_record_with_a_non_http_url_reads_as_not_running()
    {
        _records.WriteText("""{"url":"file:///tmp/sindri/","pid":4242,"epoch":7}""");
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Contains("can't be read", _vm.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_ledger_at_the_same_epoch_restarts_the_stream_from_the_start()
    {
        _records.Write(A.Record(pid: 4242, epoch: 1));
        _server.Pong = new(4242, 1);
        _server.Current = A.Status(epoch: 1);
        _server.Push(A.Event(30));
        Start();
        await Eventually(() => _vm.LastEvent.EndsWith("#30", StringComparison.Ordinal), "event 30");

        _server.Pong = new(5151, 1);
        _server.Current = A.Status(pid: 5151, epoch: 1);
        _records.Write(A.Record(pid: 5151, epoch: 1));
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Lease == "pid 5151 · epoch 1";
        }, "followed the new process");
        Assert.Equal(0, _server.StreamCursors[^1]);
    }

    [Fact]
    public async Task A_profile_sindri_says_needs_approval_never_reads_as_approved()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.Approved() with { NeedsApproval = true });
        Start();

        await Eventually(() => _vm.Headline == "Running", "running");
        Assert.Equal("needs approval", _vm.ProfileState);
        Assert.Equal("sindri profile approve", _vm.ProfileFix);
    }

    [Fact]
    public async Task An_unreadable_live_profile_offers_no_approve_fix_because_approving_it_cant_work()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: new(Approved: null, Live: null, ChangedSinceApproval: false, Problem: "SND-PROFILE-002 bad key", Source: null, NeedsApproval: true));
        Start();

        await Eventually(() => _vm.ProfileState == "unreadable", "profile state");
        Assert.Equal("", _vm.ProfileFix);
        Assert.Equal("SND-PROFILE-002 bad key", _vm.ProfileDetail);
    }

    [Fact]
    public async Task A_record_that_turns_unreadable_during_a_follow_ends_the_follow()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");

        _records.FailNextRead(new UnauthorizedAccessException("Access to the path is denied."));
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Headline == "Running" && _server.StreamCursors.Count >= 2;
        }, "the watcher ended the follow and the app reconnected");
    }

    // §4.1: stale is "no event or ping for 30 s". A control plane with nothing scheduled (an unapproved profile) emits no
    // events, but it answers ping, so the views stay fresh however long it stays quiet.
    [Fact]
    public async Task A_quiet_control_plane_that_answers_ping_never_goes_stale()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        var pings = _server.PingCalls;

        for (var second = 1; second <= 90; second++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            var due = pings + (second / 10);
            await Eventually(() => _server.PingCalls >= due, "a ping every 10 s");
            Assert.DoesNotContain("stale", _vm.Pill.Text, StringComparison.Ordinal);
        }

        Assert.Equal(ShellState.Normal, _vm.State);
    }

    [Fact]
    public async Task Pings_that_fail_for_30_seconds_go_stale()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        _server.FailPing = true;

        for (var second = 1; second < 30; second++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5, TestContext.Current.CancellationToken);
            Assert.Equal(ShellState.Normal, _vm.State);
        }

        _clock.Advance(TimeSpan.FromSeconds(1));
        await Eventually(() => _vm.State == ShellState.Stale, "stale after 30 s of failed pings");
        Assert.Equal("stale · updated 30s ago", _vm.Pill.Text);
    }

    // A ping answered by another process or epoch isn't the control plane this follow connected to: it doesn't count.
    [Fact]
    public async Task A_ping_from_another_process_does_not_keep_the_views_fresh()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        _server.Pong = new(5151, 9);

        for (var second = 1; second <= 30; second++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        await Eventually(() => _vm.State == ShellState.Stale, "stale: the pings came from someone else");
    }

    // A good ping after a stale spell freshens the views again.
    [Fact]
    public async Task A_ping_that_answers_again_freshens_a_stale_view()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        _server.FailPing = true;
        _clock.Advance(HealthViewModel.StaleAfter);
        await Eventually(() => _vm.State == ShellState.Stale, "stale");

        _server.FailPing = false;
        await Eventually(() =>
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            return _vm.State == ShellState.Normal;
        }, "fresh again after a good ping");
    }
}
