using Aw.Kernel;

namespace Aw.CodeIndex.Core.Ports;

/// <summary>
/// File contents by git object id, from one long-lived 'git cat-file --batch' per build: no file is read from the working
/// tree, so there is no symlink or race to defend against (spec §5.1). Disposing ends the process.
/// </summary>
public interface IBlobReader : IAsyncDisposable
{
    /// <summary>Reads one blob.</summary>
    /// <param name="blob">The git object id, as listed in the inventory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bytes, or null when the object is not in the store (a promisor clone: counted as 'missing', never fetched from a network).</returns>
    public ValueTask<Result<byte[]?>> Read(string blob, CancellationToken cancellationToken);
}
