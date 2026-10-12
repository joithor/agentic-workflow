using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>Asks the instance's workflow for its view (the 'GetInstance' Query); SND-WF-001 when there is no such instance.</summary>
/// <param name="runtime">The instances on Temporal.</param>
internal sealed class GetInstanceService(IInstanceRuntime runtime) : IService<GetInstanceRequest, GetInstanceResponse>
{
    public async ValueTask<Result<GetInstanceResponse>> Handle(GetInstanceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var view = await runtime.Get(request.Address, cancellationToken).ConfigureAwait(false);
        return view.IsError ? view.Error : new GetInstanceResponse(view.Value);
    }
}
