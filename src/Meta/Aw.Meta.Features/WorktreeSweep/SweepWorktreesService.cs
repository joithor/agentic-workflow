using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.WorktreeSweep;

/// <summary>Removes the worktrees whose verdict is removable: all of them, or one pull request's.</summary>
internal sealed class SweepWorktreesService : IService<SweepWorktreesRequest, SweepWorktreesResponse>
{
    public ValueTask<Result<SweepWorktreesResponse>> Handle(SweepWorktreesRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
