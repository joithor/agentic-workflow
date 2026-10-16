using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Scheduling;

/// <summary>The services a human paused (API P6), read from the 'services' view; the scheduler reads it once per tick and runs none of their slots.</summary>
public interface IServicePauses
{
    /// <summary>The paused services.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Their names.</returns>
    public ValueTask<Result<IReadOnlySet<ServiceName>>> Paused(CancellationToken cancellationToken);
}
