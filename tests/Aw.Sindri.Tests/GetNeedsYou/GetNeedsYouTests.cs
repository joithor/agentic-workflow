using Aw.Sindri.Features.GetNeedsYou;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetNeedsYou;

public sealed class GetNeedsYouTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetNeedsYou_lists_rows_by_urgency_then_since_then_id()
    {
        var service = new GetNeedsYouService();
        var request = A.GetNeedsYouRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert lists_rows_by_urgency_then_since_then_id");
    }
}
