using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetItemTimeline;

internal sealed class GetItemTimelineService : IService<GetItemTimelineRequest, TimelineResponse>
{
    public ValueTask<Result<TimelineResponse>> Handle(GetItemTimelineRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetItemTimeline: make GetItemTimelineTests pass");
}
