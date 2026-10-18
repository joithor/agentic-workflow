using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.GetStatus;

/// <summary>The two profile reads the status makes: the approved profile and the live file.</summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="source">The live file.</param>
internal sealed class ProfileReads(IApprovedProfile approvals, IProfileSource source)
{
    internal ValueTask<Result<LoadedProfile?>> Approved(CancellationToken cancellationToken) => approvals.Latest(cancellationToken);

    internal ValueTask<Result<LoadedProfile>> Live(CancellationToken cancellationToken) => source.Load(cancellationToken);
}
