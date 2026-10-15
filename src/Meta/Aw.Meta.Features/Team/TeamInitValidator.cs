using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.Team;

internal sealed class TeamInitValidator : IValidator<TeamInitRequest>
{
    public Result<Unit> Validate(TeamInitRequest value) => throw new NotSupportedException();
}
