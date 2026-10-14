using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetItem;

internal sealed class GetItemService : IService<GetItemRequest, ItemView>
{
    public ValueTask<Result<ItemView>> Handle(GetItemRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetItem: make GetItemTests pass");
}
