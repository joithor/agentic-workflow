using System.Globalization;
using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;

namespace Aw.Packets.App.Infrastructure;

/// <summary>
/// Reads a line packet directory from disk: the files the kind lists and every file under its linked directories (a
/// symbolic link, a linked file or a linked directory, is refused rather than followed), and the names of every other
/// top-level entry. Nothing else is read.
/// </summary>
internal sealed class FileSystemLinePacketSource : ILinePacketSource
{
    public ValueTask<Result<LinePacketFiles>> Read(PacketKind kind, string packetDirectory, CancellationToken cancellationToken)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(packetDirectory));
        return ValueTask.FromResult(Directory.Exists(root)
            ? ReadDirectory(kind, root)
            : PacketsErrors.NotFound.With($"No packet directory at {packetDirectory}."));
    }

    private static Result<LinePacketFiles> ReadDirectory(PacketKind kind, string root)
    {
        var present = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        foreach (var spec in kind.Files)
        {
            var bytes = FileSystemPacketSource.ReadFile(root, spec.Name);
            if (bytes.IsError)
            {
                return bytes.Error;
            }

            if (bytes.Value is { } found)
            {
                present[spec.Name] = found;
            }
        }

        try
        {
            foreach (var directory in kind.Directories)
            {
                var read = ReadLinkedDirectory(root, directory, present);
                if (read.IsError)
                {
                    return read.Error;
                }
            }

            var unlisted = new DirectoryInfo(root).EnumerateFileSystemInfos()
                .Where(entry => !kind.Files.Any(f => string.Equals(f.Name, entry.Name, StringComparison.Ordinal))
                    && !(entry is DirectoryInfo && kind.Directories.Any(d => string.Equals(d.Name, entry.Name, StringComparison.Ordinal))))
                .Select(static entry => entry is DirectoryInfo && entry.LinkTarget is null ? entry.Name + "/" : entry.Name)
                .Order(StringComparer.Ordinal)
                .ToList();
            return new LinePacketFiles(present, unlisted);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PacketsErrors.Unreadable.With($"The packet directory can't be listed: {ex.Message}");
        }
    }

    private static Result<Unit> ReadLinkedDirectory(string root, PacketLinkedDirectory directory, Dictionary<string, ReadOnlyMemory<byte>> present)
    {
        var path = Path.Combine(root, directory.Name);
        if (new DirectoryInfo(path) is { Exists: true, LinkTarget: not null })
        {
            return PacketsErrors.Malformed.With($"'{directory.Name}' is a symbolic link; packet files and directories are regular ones.");
        }

        if (!Directory.Exists(path))
        {
            return Unit.Value;
        }

        var linked = present.Keys.Count(static k => k.Contains('/', StringComparison.Ordinal));
        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            if (linked >= LineLimits.MaxLinkedFiles)
            {
                return PacketsErrors.Malformed.With(string.Create(CultureInfo.InvariantCulture, $"{directory.Name}/ holds more than {LineLimits.MaxLinkedFiles} files in all; a packet holds at most that many: split the proof."));
            }

            var bytes = FileSystemPacketSource.ReadFile(root, relative);
            if (bytes.IsError)
            {
                return bytes.Error;
            }

            if (bytes.Value is { } found)
            {
                present[relative] = found;
                linked++;
            }
        }

        return Unit.Value;
    }
}
