using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CloseItem;

internal sealed class CloseItemService : IService<CloseItemRequest, CommandAccepted>
{
    public ValueTask<Result<CommandAccepted>> Handle(CloseItemRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("CloseItem: make CloseItemTests pass");
}
