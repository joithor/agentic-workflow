using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.GetProfile;

/// <summary>GET /profile (API spec §3.1): the 'profile' view's one row.</summary>
public sealed class GetProfileTests
{
    private static readonly ProfileHash _a = ProfileHash.From(new string('a', 64));
    private static readonly ProfileHash _b = ProfileHash.From(new string('b', 64));
    private readonly ViewsHarness _v = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task GetProfile_shows_approved_and_live_hash_and_the_changed_paths()
    {
        var approvedAt = _v.Slice.Clock.GetUtcNow();
        await _v.Fold(EventTypes.ProfileApproved, new ProfileApproved(_a, []));
        _v.Slice.Clock.Advance(TimeSpan.FromSeconds(30));
        await _v.Fold(EventTypes.ProfileChecked, new ProfileChecked(_v.Slice.Ids.New<CommandId>(), _a, _b, Drifted: true,
            ["services.heartbeat.every"], [ServiceName.From("heartbeat")]));

        var result = await Service().Handle(A.GetProfileRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        var profile = result.Value;
        Assert.Equal((_a, approvedAt, _b, approvedAt.AddSeconds(30), true), (profile.Approved, profile.ApprovedAt, profile.Live, profile.CheckedAt, profile.Changed));
        Assert.Equal(["services.heartbeat.every"], profile.ChangedPaths);
        Assert.Equal(_v.Progress.Current.AsOf, profile.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Before_any_event_it_answers_nulls_and_not_changed()
    {
        var profile = (await Service().Handle(new GetProfileRequest(), Token)).Value;

        Assert.Equal((null, null, null, null, false), (profile.Approved, profile.ApprovedAt, profile.Live, profile.CheckedAt, profile.Changed));
        Assert.Empty(profile.ChangedPaths);
    }

    private GetProfileService Service() => new(_v.Views, _v.Progress);
}
