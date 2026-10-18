using System.Globalization;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The bound unix sockets from &lt;proc root&gt;/net/unix (collector spec CI-3): how a tmux server's socket path is found without calling tmux.</summary>
/// <param name="root">The /proc directory (or a fixture tree).</param>
internal sealed class UnixSockets(string root) : IUnixSockets
{
    private const int InodeColumn = 6;
    private const int PathColumn = 7;

    public IReadOnlyDictionary<long, string> Read()
    {
        var found = new Dictionary<long, string>();
        try
        {
            foreach (var line in File.ReadLines(Path.Combine(root, "net", "unix")).Skip(1))
            {
                var fields = line.Split(' ', PathColumn + 1, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length > PathColumn && long.TryParse(fields[InodeColumn], NumberStyles.None, CultureInfo.InvariantCulture, out var inode))
                {
                    found[inode] = fields[PathColumn].Trim();
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new Dictionary<long, string>();
        }

        return found;
    }
}
