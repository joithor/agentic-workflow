using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.ProbeLaunchTargets;

/// <summary>The work of a probe job (UI spec E-3): checks the viewer, the host, every catalog target and every repo, and records what changed.</summary>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="probes">Probes and records.</param>
internal sealed class ProbeLaunchTargetsService(HeldLease held, ILaunchProbes probes) : IService<ProbeLaunchTargetsRequest, ProbeLaunchTargetsResult>
{
    public ValueTask<Result<ProbeLaunchTargetsResult>> Handle(ProbeLaunchTargetsRequest request, CancellationToken cancellationToken)
    {
        _ = (held, probes);
        throw new NotSupportedException("ProbeLaunchTargets: make ProbeLaunchTargetsTests pass");
    }
}
