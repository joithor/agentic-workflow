using Aw.Kernel.Ids;
using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetStatus;

/// <summary>GET /status serves 'views' (the projector's status and its sentence, API spec §2.3) and 'health' (UI spec E-14).</summary>
public sealed class StatusViewsHealthTests
{
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SliceHarness H => _v.Slice;

    private GetStatusService Service() => new(H.Held, H.Profiles, H.Profiles, H.Store, H.Events, _v.Progress, _v.Views);

    private async Task RunningAndApproved()
    {
        var approved = SliceHarness.Profile("5s");
        H.Profiles.Live = approved;
        _ = await H.HoldLease();
        _ = await new ApproveProfileService(H.Profiles, H.Profiles, H.Held, H.Store, H.Events, H.Challenges, FakeContextEngine.Instance).Handle(new ApproveProfileRequest(approved.Hash, H.Confirm(approved.Hash)), Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_reports_views_and_health()
    {
        await RunningAndApproved();
        _v.Publish(stalledAt: null);

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(_v.Progress.Current, status.Views);
        Assert.Null(status.Views!.Text);
        Assert.Equal(new HealthReport(HealthWord.Healthy, [], Sentence: null), status.Health);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_stalled_projector_is_stalled_health_with_the_sentence_beside_it()
    {
        await RunningAndApproved();
        _v.Publish(stalledAt: 2);

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(EmptyText.Stalled(2), status.Views!.Text);
        Assert.Equal(HealthWord.Stalled, status.Health!.Word);
        Assert.Equal("Sindri is stalled: the views stopped at event 2.", status.Health.Sentence);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_is_rebuilding_health()
    {
        await RunningAndApproved();
        _v.Progress.RequestRebuild();

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(EmptyText.Rebuilding(0, 0), status.Views!.Text);
        Assert.Equal(HealthWord.Rebuilding, status.Health!.Word);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_service_and_an_unapproved_profile_degrade_it()
    {
        await _v.Fold(EventTypes.ServiceRan, new ServiceRan(H.Ids.New<CommandId>(), ServiceName.From("heartbeat"), H.Clock.GetUtcNow(), ServiceOutcome.Failed, "SND-SCHED-001"));

        var status = (await Service().Handle(new GetStatusRequest(), Token)).Value;

        Assert.Equal(HealthWord.Degraded, status.Health!.Word);
        Assert.Equal(["SND-API-001", "SND-SCHED-001", "SND-PROFILE-005"], status.Health.Problems.Select(p => p.Code), StringComparer.Ordinal);
        Assert.Equal("Sindri is degraded: Sindri isn't running, service heartbeat failed, and the profile isn't approved.", status.Health.Sentence);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_views_json_carries_the_status_fields_and_text_without_a_nested_as_of()
    {
        var views = new ViewsStatus(Cursor: 6, LedgerHead: 9, ProjectedAt: null, Rebuilding: false, Stalled: true, StalledAt: 7, StalledType: "service.ran");

        var json = System.Text.Encoding.UTF8.GetString(Aw.Kernel.Json.AwJson.Serialize(views, SliceHarness.Json));

        Assert.Contains("\"cursor\":6", json, StringComparison.Ordinal);
        Assert.Contains("\"stalledType\":\"service.ran\"", json, StringComparison.Ordinal);
        Assert.Contains("\"text\":\"Views are stalled at 7: run `sindri views rebuild`\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("asOf", json, StringComparison.Ordinal);
    }
}
