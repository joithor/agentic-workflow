using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Readiness.App.Features.CheckRepo;

internal sealed class CheckRepoService : IService<CheckRepoRequest, CheckRepoResponse>
{
    public ValueTask<Result<CheckRepoResponse>> Handle(CheckRepoRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("CheckRepo: make CheckRepoTests pass");
}
