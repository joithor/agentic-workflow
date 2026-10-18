using Aw.Kernel;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The managed Onyx's notice for the needs-you list, read from the approved profile and the files `onyx up` and `serve` keep.</summary>
public interface IOnyxNotices
{
    /// <summary>Reads the current notice.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The notice, or null when nothing needs the human.</returns>
    public ValueTask<Result<OnyxNotice?>> Current(CancellationToken cancellationToken);
}
