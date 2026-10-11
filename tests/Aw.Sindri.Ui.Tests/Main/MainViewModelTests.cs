using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Main;

[Lane(TestLane.Unit)]
public sealed class MainViewModelTests : IDisposable
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly MainViewModel _vm;

    public MainViewModelTests()
    {
        _vm = new MainViewModel(_records, _server, _clock);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private void Start() => _ = _vm.Run(_stop.Token);

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
            _clock.Advance(MainViewModel.RetryEvery);
            return _vm.Lease == "pid 4242 · epoch 8";
        }, "reconnected");
        Assert.Equal(30, _server.StreamCursors[^1]);
    }
}
