using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListRequests;

#pragma warning disable CS9113 // Parameter is unread: the stub that the red tests compile against
internal sealed class ListRequestsService(IViewReader views, IItemResolver items, IProjectorStatus status, ICallerContextAccessor callers) : IService<ListRequestsRequest, ListRequestsResponse>
{
    public ValueTask<Result<ListRequestsResponse>> Handle(ListRequestsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ListRequests: make ListRequestsTests pass");
}
