using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Messages;

namespace Aw.Meta.Features.Roles;

internal sealed class SetRoleService : IService<SetRoleRequest, MetaRole>
{
    public ValueTask<Result<MetaRole>> Handle(SetRoleRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
