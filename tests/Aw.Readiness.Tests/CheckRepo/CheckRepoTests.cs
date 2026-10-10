using Aw.Readiness.App.Features.CheckRepo;
using Aw.Readiness.Testing;

namespace Aw.Readiness.Tests.CheckRepo;

public sealed class CheckRepoTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task CheckRepo_reports_level_per_pillar()
    {
        var service = new CheckRepoService();
        var request = A.CheckRepoRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert reports_level_per_pillar");
    }
}
