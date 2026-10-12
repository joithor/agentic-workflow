using Aw.Sindri.Features.ListServices;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ListServices;

public sealed class ListServicesTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ListServices_lists_each_approved_service_with_its_last_run()
    {
        var service = new ListServicesService();
        var request = A.ListServicesRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert lists_each_approved_service_with_its_last_run");
    }
}
