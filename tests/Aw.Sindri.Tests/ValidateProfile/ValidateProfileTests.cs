using Aw.Sindri.Features.ValidateProfile;

namespace Aw.Sindri.Tests.ValidateProfile;

public sealed class ValidateProfileTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task ValidateProfile_returns_the_ledger_error_when_the_recorded_approval_cant_be_read()
    {
        var live = SliceHarness.Profile("5s");
        _h.Profiles.Live = live;
        _ = await _h.Profiles.Save(live, Token);
        var service = new ValidateProfileService(_h.Profiles, _h.Profiles, new ReadFailingLedger(_h.Store), _h.Events);

        var validated = await service.Handle(new ValidateProfileRequest(), Token);

        Assert.Equal(ReadFailingLedger.Failure, validated.Error);
    }
}
