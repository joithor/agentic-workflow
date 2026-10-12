using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListServices;

internal sealed class ListServicesService(IApprovalReader approvals, IViewReader views, IProjectorStatus status) : IService<ListServicesRequest, ListServicesResponse>
{
    public ValueTask<Result<ListServicesResponse>> Handle(ListServicesRequest request, CancellationToken cancellationToken)
    {
        _ = (approvals, views, status);
        throw new NotSupportedException("ListServices: make ListServicesTests pass");
    }
}
