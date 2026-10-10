using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Readiness.Core.Ports;

namespace Aw.Readiness.App.Features.CheckRepo;

internal sealed class CheckRepoService(IRepoReader reader, IRepoHost host, IRunHistory history, TimeProvider clock)
    : IService<CheckRepoRequest, CheckRepoResponse>
{
    public ValueTask<Result<CheckRepoResponse>> Handle(CheckRepoRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException($"red: CheckRepo over {reader}, {host}, {history}, {clock}");
}
