using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.OwnerAsk;

internal sealed class AskOwnerService : IService<AskOwnerRequest, AskOwnerResponse>
{
    public ValueTask<Result<AskOwnerResponse>> Handle(AskOwnerRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
