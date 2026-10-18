using Aw.Kernel;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core;

/// <summary>Reads the sizes file 'validate --sizes' names.</summary>
public interface ISizesSource
{
    /// <summary>Reads and parses the sizes file at <paramref name="path"/>.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sizes; AW-PACKETS-009 when the file is missing, unreadable, malformed or not schemaVersion 1.</returns>
    public ValueTask<Result<SizesFile>> Read(string path, CancellationToken cancellationToken);
}
