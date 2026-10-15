using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.Owner;

internal sealed class ListOwnerService : IService<ListOwnerRequest, ListOwnerResponse>
{
    public ValueTask<Result<ListOwnerResponse>> Handle(ListOwnerRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
