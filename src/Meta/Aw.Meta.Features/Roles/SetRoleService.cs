using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Roles;

/// <summary>Registers the meta session; a second registration replaces the first.</summary>
/// <param name="messages">Stores the role.</param>
internal sealed class SetRoleService(IMessageStore messages) : IService<SetRoleRequest, MetaRole>
{
    public async ValueTask<Result<MetaRole>> Handle(SetRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var role = new MetaRole(request.Session, request.Name.Trim());
        var saved = await messages.SetRole(role, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : role;
    }
}
