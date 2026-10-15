using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Roles;

/// <summary>Reads the registered meta session.</summary>
/// <param name="messages">Stores the role.</param>
internal sealed class ShowRoleService(IMessageStore messages) : IService<ShowRoleRequest, ShowRoleResponse>
{
    public async ValueTask<Result<ShowRoleResponse>> Handle(ShowRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await messages.Role(cancellationToken).ConfigureAwait(false);
        return role.IsError ? role.Error : new ShowRoleResponse(role.Value);
    }
}
