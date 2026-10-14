using Aw.Sindri.Features.GetItemTimeline;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.GetItemTimeline;

public sealed class GetItemTimelineTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task GetItemTimeline_lists_the_items_events_after_a_cursor()
    {
        var service = new GetItemTimelineService();
        var request = A.GetItemTimelineRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert lists_the_items_events_after_a_cursor");
    }
}
