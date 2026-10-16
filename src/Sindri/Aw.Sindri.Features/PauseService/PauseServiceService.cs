using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.PauseService;

/// <summary>Pauses a service (API P6 unit D): SND-SVC-404 for an unknown one, then one 'service.paused'. Red until then.</summary>
internal sealed class PauseServiceService : IService<PauseServiceRequest, CommandAccepted>
{
    public ValueTask<Result<CommandAccepted>> Handle(PauseServiceRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit D");
}
