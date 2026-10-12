using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The read-only half of <see cref="IApprovals"/>: the latest approved profile. Query handlers take this, never the
/// write port (ARCH-CQRS-002).
/// </summary>
public interface IApprovedProfile
{
    /// <summary>The most recently approved profile, or null when none was approved.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The snapshot.</returns>
    public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken);
}
