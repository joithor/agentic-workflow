using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CancelJob;

/// <summary>Cancels a job (API P6 unit E): JobRules.CanCancel, then 'job.cancelled' and the token. Red until then.</summary>
internal sealed class CancelJobService : IService<CancelJobRequest, CommandAccepted>
{
    public ValueTask<Result<CommandAccepted>> Handle(CancelJobRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit E");
}
