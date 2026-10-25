using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>Whether the approved profile says Sindri manages Onyx (`context.sources.onyx.managed: true`); an unmanaged Onyx (the manual setup) is never touched.</summary>
public interface IManagedOnyx
{
    /// <summary>Reads the latest approved profile.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the approved profile names a managed Onyx; false when it doesn't or there is no approved profile.</returns>
    public ValueTask<Result<bool>> Is(CancellationToken cancellationToken);

    /// <summary>Reads the latest approved profile.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when the approved profile names any Onyx at all, managed or the owner's own; the first-run offer is made only when it names none.</returns>
    public ValueTask<Result<bool>> IsConfigured(CancellationToken cancellationToken);
}
