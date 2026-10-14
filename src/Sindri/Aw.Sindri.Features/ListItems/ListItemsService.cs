using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListItems;

internal sealed class ListItemsService : IService<ListItemsRequest, ListItemsResponse>
{
    public ValueTask<Result<ListItemsResponse>> Handle(ListItemsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ListItems: make ListItemsTests pass");
}
