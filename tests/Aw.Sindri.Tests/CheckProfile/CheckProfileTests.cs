using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CheckProfile;

namespace Aw.Sindri.Tests.CheckProfile;

/// <summary>The profile check (API spec C8): 'profile.checked' only when the (approved, live) pair changes.</summary>
public sealed class CheckProfileTests
{
    private readonly SliceHarness _h = new();
    private readonly LoadedProfile _a = SliceHarness.Profile("30s");
    private readonly LoadedProfile _b = SliceHarness.Profile("5s");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Profile_check_appends_only_when_the_pair_changes()
    {
        var grant = await Approve(_a);
        _h.Profiles.Live = _a;

        var first = await Check(grant);
        var again = await Check(grant);

        _ = Assert.NotNull(first.Sequence);
        Assert.Null(again.Sequence);
        var check = Assert.Single(Checks());
        Assert.Equal((_a.Hash, _a.Hash, false), (check.Approved, check.Live, check.Drifted));
        Assert.Equal(["heartbeat"], check.ApprovedServices.Select(s => s.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_drift_names_the_changed_paths_and_a_revert_writes_drifted_false()
    {
        var grant = await Approve(_a);
        _h.Profiles.Live = _b;
        _ = await Check(grant);
        _h.Profiles.Live = _a;

        _ = await Check(grant);

        Assert.Equal([(_b.Hash, true), (_a.Hash, false)], Checks().Select(c => (c.Live!.Value, c.Drifted)));
        Assert.Equal(["services.heartbeat.every"], Checks()[0].ChangedPaths);
        Assert.Empty(Checks()[1].ChangedPaths);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Drifting_back_to_an_old_hash_writes_again()
    {
        var grant = await Approve(_a);
        foreach (var live in new[] { _a, _b, _a, _b })
        {
            _h.Profiles.Live = live;
            _ = await Check(grant);
        }

        Assert.Equal([false, true, false, true], Checks().Select(c => c.Drifted));
        Assert.Equal(["profile.checked:0", "profile.checked:1", "profile.checked:2", "profile.checked:3"],
            _h.Store.Events.Where(e => e.Envelope.Type == EventTypes.ProfileChecked).Select(e => e.IdempotencyKey), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_live_profile_is_live_null_and_drifted_when_approved()
    {
        var grant = await Approve(_a);   // the harness's live profile is missing

        _ = await Check(grant);

        var check = Assert.Single(Checks());
        Assert.Equal((_a.Hash, true), (check.Approved, check.Drifted));
        Assert.Null(check.Live);
        Assert.Empty(check.ChangedPaths);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_nothing_approved_a_check_is_not_drifted()
    {
        var grant = await _h.HoldLease();
        _h.Profiles.Live = _a;

        _ = await Check(grant);

        Assert.True(Assert.Single(Checks()) is { Approved: null, Drifted: false, ApprovedServices.Count: 0 });
    }

    private async Task<LeaseGrant> Approve(LoadedProfile profile)
    {
        _ = await _h.Profiles.Save(profile, Token);
        return await _h.HoldLease();
    }

    private async Task<CheckProfileResponse> Check(LeaseGrant grant)
    {
        var service = new CheckProfileService(_h.Profiles, _h.Profiles, _h.Store, _h.Events, _h.Callers, _h.Ids);
        return (await service.Handle(new CheckProfileRequest(grant), Token)).Value;
    }

    private List<ProfileChecked> Checks() => [.. _h.Store.Events
        .Where(e => e.Envelope.Type == EventTypes.ProfileChecked)
        .Select(e => _h.Events.Payload<ProfileChecked>(e).Value)];
}
