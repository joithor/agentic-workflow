using Aw.Sindri.Features.Decide;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.DecideCommand;

public sealed class DecideTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Decide_appends_decision_recorded_by_caller()
    {
        var service = new DecideService();
        var request = A.DecideRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert appends_decision_recorded_by_caller");
    }
}
