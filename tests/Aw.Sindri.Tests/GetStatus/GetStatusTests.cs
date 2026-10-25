using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Features.RecordHeartbeat;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetStatus;

public sealed class GetStatusTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private GetStatusService Service() => new(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles), _h.Store, _h.Events, new ProjectorProgress(_h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_reports_not_running_when_no_lease_is_held()
    {
        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.False(status.Running);
        Assert.Equal(0, status.Epoch);
        Assert.Null(status.Profile.Approved);
        Assert.True(status.Profile.NeedsApproval);
        Assert.Empty(status.Services);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_reports_api_version_1() =>
        Assert.Equal(1, (await Service().Handle(new GetStatusRequest(), Token)).Value.ApiVersion);

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_reads_the_ledger_head_fresh_while_the_cursor_stays_the_projectors()
    {
        var grant = await _h.HoldLease();
        _ = await new RecordHeartbeatService(_h.Store, _h.Events, _h.Clock).Handle(new RecordHeartbeatRequest(new ScheduledRun(grant, _h.Clock.GetUtcNow())), Token);
        var head = (await _h.Store.Head(Token)).Value;

        var views = (await Service().Handle(new GetStatusRequest(), Token)).Value.Views;   // the projector hasn't seen any of it

        Assert.True(head > 0);
        Assert.Equal((0L, head), (views!.Cursor, views.LedgerHead));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_reports_epoch_services_and_the_last_heartbeat()
    {
        var approved = SliceHarness.Profile("5s");
        _h.Profiles.Live = approved;
        var grant = await _h.HoldLease();
        _ = await new ApproveProfileService(_h.Profiles, _h.Profiles, _h.Held, _h.Store, _h.Events, new ProfileChecks(FakeContextEngine.Instance, new FixedAgentCatalog())).Handle(new ApproveProfileRequest(approved.Hash, _h.Confirm(approved.Hash)), Token);
        _ = await new RecordHeartbeatService(_h.Store, _h.Events, _h.Clock).Handle(new RecordHeartbeatRequest(new ScheduledRun(grant, _h.Clock.GetUtcNow())), Token);

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.True(status.Running);
        Assert.Equal(grant.Epoch, status.Epoch);
        Assert.Equal(approved.Hash, status.Profile.Approved);
        Assert.False(status.Profile.NeedsApproval);
        var heartbeat = Assert.Single(status.Services);
        Assert.Equal(("heartbeat", "5s", _h.Clock.GetUtcNow()), (heartbeat.Name.Value, heartbeat.Every, heartbeat.LastRun!.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_flags_a_profile_changed_since_approval_and_keeps_the_approved_cadence()
    {
        _ = await _h.Profiles.Save(SliceHarness.Profile("5s"), Token);
        _h.Profiles.Live = SliceHarness.Profile("7s");

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.True(status.Profile.ChangedSinceApproval);
        Assert.True(status.Profile.NeedsApproval);
        Assert.Equal("5s", Assert.Single(status.Services).Every);
        Assert.Equal(HeartbeatService.ServiceId, Assert.Single(status.Services).Name);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_returns_the_ledger_error_when_the_recorded_approval_cant_be_read()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.Profiles.Save(live, Token);

        var status = await new GetStatusService(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles), new ReadFailingLedger(_h.Store), _h.Events, new ProjectorProgress(_h.Clock), new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot()).Handle(new GetStatusRequest(), Token);

        Assert.Equal(ReadFailingLedger.Failure.Code, status.Error.Code);   // an error (exit 2), never "needs approval"
    }
}
