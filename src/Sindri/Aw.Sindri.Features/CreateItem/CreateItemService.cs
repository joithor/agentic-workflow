using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CreateItem;

internal sealed class CreateItemService : IService<CreateItemRequest, ItemCreatedResponse>
{
    public ValueTask<Result<ItemCreatedResponse>> Handle(CreateItemRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("CreateItem: make CreateItemTests pass");
}
