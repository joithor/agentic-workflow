using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The read-only side of the approved profile snapshots (API spec C9): what queries may take (ARCH-CQRS-002).</summary>
public interface IApprovalReader
{
    /// <summary>The most recently approved profile, or null when none was approved.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The snapshot.</returns>
    public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken);
}
