using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>
/// Resolves the instance a reference names (its id, its address, or a bare item: spec §6.1, §6.3), then asks its
/// workflow for its view (the 'GetInstance' Query); SND-WF-001 when there is no such instance.
/// </summary>
/// <param name="directory">Resolves the reference to the instance's address.</param>
/// <param name="runtime">The instances on Temporal.</param>
internal sealed class GetInstanceService(IInstanceDirectory directory, IInstanceReader runtime) : IService<GetInstanceRequest, GetInstanceResponse>
{
    public async ValueTask<Result<GetInstanceResponse>> Handle(GetInstanceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var address = await directory.Resolve(request.Instance, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return address.Error;
        }

        var view = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        return view.IsError ? view.Error : new GetInstanceResponse(view.Value);
    }
}
