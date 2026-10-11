using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetStatus;

public sealed class GetStatusTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetStatus_reports_not_running_when_no_lease_is_held()
    {
        var service = new GetStatusService();
        var request = A.GetStatusRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert reports_not_running_when_no_lease_is_held");
    }
}
