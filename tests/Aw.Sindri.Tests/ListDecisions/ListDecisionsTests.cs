using Aw.Sindri.Features.ListDecisions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ListDecisions;

public sealed class ListDecisionsTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ListDecisions_lists_decisions_with_options_filtered_by_pending_and_item()
    {
        var service = new ListDecisionsService();
        var request = A.ListDecisionsRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert lists_decisions_with_options_filtered_by_pending_and_item");
    }
}
