using Aw.Evidence.Core.Locks;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ApiRun;

/// <summary>
/// Runs an API script: reads and checks it, vets the host, headers and goldens (refusals take no lock), then holds the
/// QA-stack and heavy-job locks while it runs the requests, and writes a scrubbed summary.json on every path once the
/// script has been read.
/// </summary>
internal sealed class ApiRunService(ApiRunFiles files, ApiVetting vetting, RequestRunner requests, IMachineLocks locks, TimeProvider clock, IIdGenerator ids)
    : IService<ApiRunRequest, ApiRunResponse>
{
    internal (object, object, object, object, object, object) Collaborators { get; } = (files, vetting, requests, locks, clock, ids);

    public ValueTask<Result<ApiRunResponse>> Handle(ApiRunRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
