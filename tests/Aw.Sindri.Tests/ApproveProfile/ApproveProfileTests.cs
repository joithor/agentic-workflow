using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.ApproveProfile;

namespace Aw.Sindri.Tests.ApproveProfile;

public sealed class ApproveProfileTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ApproveProfileService Service() => new(_h.Profiles, _h.Profiles, _h.Held, _h.Store, _h.Events);

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveProfile_without_a_hash_shows_the_hash_and_changes_and_records_nothing()
    {
        _h.Profiles.Live = SliceHarness.Profile("5s");
        _ = await _h.HoldLease();

        var shown = (await Service().Handle(new ApproveProfileRequest(Hash: null, Confirmed: false), Token)).Value;

        Assert.Equal(_h.Profiles.Live.Value.Hash, shown.Hash);
        Assert.False(shown.Approved);
        Assert.Contains("services.heartbeat.every", shown.Changes, StringComparer.Ordinal);
        Assert.Null((await _h.Profiles.Latest(Token)).Value);
        Assert.Empty(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveProfile_with_the_live_hash_and_confirmation_saves_and_records_it()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();

        var approved = (await Service().Handle(new ApproveProfileRequest(live.Hash, Confirmed: true), Token)).Value;

        Assert.True(approved.Approved);
        Assert.Equal(live.Hash, (await _h.Profiles.Latest(Token)).Value!.Hash);
        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal((EventTypes.ProfileApproved, $"profile.approved:{live.Hash.Value}"), (recorded.Envelope.Type, recorded.IdempotencyKey));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("wrong-hash", true, "SND-PROFILE-003")]
    [InlineData("live", false, "SND-PROFILE-004")]
    public async Task ApproveProfile_refuses_a_stale_hash_or_a_missing_confirmation(string which, bool confirmed, string code)
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.HoldLease();
        var hash = which == "live" ? live.Hash : SliceHarness.Profile("9s").Hash;

        var refused = await Service().Handle(new ApproveProfileRequest(hash, confirmed), Token);

        Assert.Equal(code, refused.Error.Code);
        Assert.Null((await _h.Profiles.Latest(Token)).Value);
        Assert.Empty(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task ApproveProfile_of_an_invalid_profile_returns_its_error()
    {
        _h.Profiles.Live = SindriErrors.ProfileInvalid.With("/p.yaml: user: must be non-empty text");

        Assert.Equal(SindriErrors.ProfileInvalid.Code, (await Service().Handle(new ApproveProfileRequest(Hash: null, Confirmed: false), Token)).Error.Code);
    }
}
