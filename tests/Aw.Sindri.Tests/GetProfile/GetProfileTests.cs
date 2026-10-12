using Aw.Sindri.Features.GetProfile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetProfile;

public sealed class GetProfileTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetProfile_shows_approved_and_live_hash_and_the_changed_paths()
    {
        var service = new GetProfileService();
        var request = A.GetProfileRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert shows_approved_and_live_hash_and_the_changed_paths");
    }
}
