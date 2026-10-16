using Aw.Sindri.Features.ProbeLaunchTargets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ProbeLaunchTargets;

public sealed class ProbeLaunchTargetsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ProbeLaunchTargets_records_a_probe()
    {
        var service = new ProbeLaunchTargetsService();
        var request = A.ProbeLaunchTargetsRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert records_a_probe");
    }
}
