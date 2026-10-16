using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RunService;

/// <summary>The work of a RunService job (API P6 unit D): runs the named service once, guarded against a scheduled run in flight. Red until then.</summary>
internal sealed class RunServiceService : IService<RunServiceRequest, ScheduledResult>
{
    public ValueTask<Result<ScheduledResult>> Handle(RunServiceRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red until API P6 unit D");
}
