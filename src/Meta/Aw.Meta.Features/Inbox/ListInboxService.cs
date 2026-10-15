using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.Inbox;

internal sealed class ListInboxService : IService<ListInboxRequest, ListInboxResponse>
{
    public ValueTask<Result<ListInboxResponse>> Handle(ListInboxRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
