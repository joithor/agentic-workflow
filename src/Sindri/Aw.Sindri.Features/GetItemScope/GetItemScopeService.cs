using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetItemScope;

internal sealed class GetItemScopeService : IService<GetItemScopeRequest, ItemScopeResponse>
{
    public ValueTask<Result<ItemScopeResponse>> Handle(GetItemScopeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
