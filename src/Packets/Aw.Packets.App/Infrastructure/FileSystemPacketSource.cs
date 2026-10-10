using System.Text.Json;
using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Ports;

namespace Aw.Packets.App.Infrastructure;

/// <summary>Reads a packet directory from disk. Links stay inside the directory; a symbolic link is refused rather than followed.</summary>
/// <param name="json">The packet file options (AwJson over PacketsJsonContext).</param>
internal sealed class FileSystemPacketSource(JsonSerializerOptions json) : IPacketSource
{
    public ValueTask<Result<LoadedPacket>> Load(string packetDirectory, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(packetDirectory);
        var result = Directory.Exists(root)
            ? PacketLoading.Load(relative => Read(root, relative), json)
            : PacketsErrors.NotFound.With($"No packet directory at {packetDirectory}.");
        return ValueTask.FromResult(result);
    }

    private static Result<ReadOnlyMemory<byte>?> Read(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return PacketsErrors.Malformed.With($"'{relative}' resolves outside the packet directory.");
        }

        try
        {
            var file = new FileInfo(full);
            if (!file.Exists)
            {
                return Results.Success<ReadOnlyMemory<byte>?>(value: null);
            }

            return file.LinkTarget is null
                ? Results.Success<ReadOnlyMemory<byte>?>(File.ReadAllBytes(full))
                : PacketsErrors.Malformed.With($"'{relative}' is a symbolic link; packet files are regular files.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PacketsErrors.Unreadable.With($"'{relative}' can't be read: {ex.Message}");
        }
    }
}
