using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Features.PlanSandbox;

/// <summary>Plans one launch through the sandbox the host registered: the product's refuses with SND-LAUNCH-014 until SB-5a.</summary>
/// <param name="sandbox">The registered sandbox.</param>
internal sealed class PlanSandboxService(ISandbox sandbox) : IService<PlanSandboxRequest, SandboxPlan>
{
    public ValueTask<Result<SandboxPlan>> Handle(PlanSandboxRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(sandbox.Plan(request.Launch));
    }
}
