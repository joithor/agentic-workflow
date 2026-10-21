using Aw.Kernel;

namespace Aw.Sindri.Core.Packets;

/// <summary>
/// Reads one file of a packet directory (the frozen scope packet, a candidate design packet: line spec LC-1, a station reads its input's files after it verified the hash).
/// Only a packet's own file names are read, never a path, and only inside Sindri's home.
/// </summary>
public interface IPacketFiles
{
    /// <summary>The text of a packet file.</summary>
    /// <param name="directory">The packet directory (absolute, inside Sindri's home).</param>
    /// <param name="name">The file's name, such as 'scope-map.json': no path.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text; an error when the directory is outside Sindri's home, the name is not a file name, or the file is not there or is too large.</returns>
    public ValueTask<Result<string>> Read(string directory, string name, CancellationToken cancellationToken);
}
