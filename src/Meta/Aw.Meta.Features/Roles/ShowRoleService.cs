using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.Roles;

internal sealed class ShowRoleService : IService<ShowRoleRequest, ShowRoleResponse>
{
    public ValueTask<Result<ShowRoleResponse>> Handle(ShowRoleRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
