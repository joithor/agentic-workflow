using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.RebuildViews;

internal sealed class RebuildViewsService : IService<RebuildViewsRequest, CommandAccepted>
{
    public ValueTask<Result<CommandAccepted>> Handle(RebuildViewsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("RebuildViews: make RebuildViewsTests pass");
}
