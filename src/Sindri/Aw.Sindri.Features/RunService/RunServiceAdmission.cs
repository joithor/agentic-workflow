using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.RunService;

/// <summary>What a RunService start checks inside the gate, so the caller sees the refusal at once (API P6 unit D). Red until then.</summary>
internal static class RunServiceAdmission
{
    internal static ValueTask<Result<ServiceName?>> Check(IServiceProvider _, RunServiceRequest request, CancellationToken __)
    {
        Result<ServiceName?> named = request.Name;
        return ValueTask.FromResult(named);
    }
}
