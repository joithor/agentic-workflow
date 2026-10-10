using Aw.Judge.Features.GetHealth;
using Aw.Judge.Testing;

namespace Aw.Judge.Tests.GetHealth;

public sealed class GetHealthTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetHealth_reports_ok_with_no_failures()
    {
        var service = new GetHealthService();
        var request = A.GetHealthRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert reports_ok_with_no_failures");
    }
}
