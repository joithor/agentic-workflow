using Aw.Kernel;

namespace Aw.Meta.Core.Ports;

/// <summary>The digest files lanes write before a recycle.</summary>
public interface IDigestFiles
{
    /// <summary>Looks at a digest file.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Its size and time, or null when there is no such file.</returns>
    public ValueTask<Result<DigestInfo?>> Stat(string path, CancellationToken cancellationToken);
}
