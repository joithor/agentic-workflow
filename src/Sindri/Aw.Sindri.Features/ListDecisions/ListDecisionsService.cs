using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListDecisions;

internal sealed class ListDecisionsService : IService<ListDecisionsRequest, ListDecisionsResponse>
{
    public ValueTask<Result<ListDecisionsResponse>> Handle(ListDecisionsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ListDecisions: make ListDecisionsTests pass");
}
