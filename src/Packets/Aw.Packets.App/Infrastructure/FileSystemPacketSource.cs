using System.Text.Json;
using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;

namespace Aw.Packets.App.Infrastructure;

/// <summary>
/// Reads a packet directory from disk. Every linked file stays inside the directory, and a symbolic link anywhere
/// between the directory and the file (a linked file or a linked sub-directory) is refused rather than followed.
/// </summary>
/// <param name="json">The packet file options (AwJson over PacketsJsonContext).</param>
internal sealed class FileSystemPacketSource(JsonSerializerOptions json) : IPacketSource
{
    public ValueTask<Result<LoadedPacket>> Load(string packetDirectory, CancellationToken cancellationToken)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(packetDirectory));
        var result = Directory.Exists(root)
            ? PacketReader.Load(relative => ReadFile(root, relative), json)
            : PacketsErrors.NotFound.With($"No packet directory at {packetDirectory}.");
        return ValueTask.FromResult(result);
    }

    // One file by its path under root, never through a symbolic link or out of the directory; shared with the scoping source.
    internal static Result<ReadOnlyMemory<byte>?> ReadFile(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var inside = Path.GetRelativePath(root, full);
        if (Path.IsPathRooted(inside) || inside == ".." || inside.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return PacketsErrors.Malformed.With($"'{relative}' resolves outside the packet directory.");
        }

        try
        {
            if (LinkOnTheWay(root, inside) is { } link)
            {
                return PacketsErrors.Malformed.With($"'{relative}' goes through the symbolic link '{link}'; packet files and directories are regular ones.");
            }

            return File.Exists(full)
                ? Results.Success<ReadOnlyMemory<byte>?>(File.ReadAllBytes(full))
                : Results.Success<ReadOnlyMemory<byte>?>(value: null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PacketsErrors.Unreadable.With($"'{relative}' can't be read: {ex.Message}");
        }
    }

    // The first path segment under root that is a symbolic link, or null.
    private static string? LinkOnTheWay(string root, string inside)
    {
        var path = root;
        var walked = new List<string>();
        foreach (var segment in inside.Split(Path.DirectorySeparatorChar))
        {
            path = Path.Combine(path, segment);
            walked.Add(segment);
            FileSystemInfo entry = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            if (entry.Exists && entry.LinkTarget is not null)
            {
                return string.Join('/', walked);
            }
        }

        return null;
    }
}
