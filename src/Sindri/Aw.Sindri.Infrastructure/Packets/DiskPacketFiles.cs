using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;

namespace Aw.Sindri.Infrastructure.Packets;

/// <summary><see cref="IPacketFiles"/> on disk: a plain file name in a directory under Sindri's home, at most 4 MB.</summary>
/// <param name="settings">Sindri's home.</param>
internal sealed partial class DiskPacketFiles(SindriSettings settings) : IPacketFiles
{
    private const long MaxBytes = 4 * 1024 * 1024;

    /// <inheritdoc/>
    public async ValueTask<Result<string>> Read(string directory, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(name);
        if (!FileName.IsMatch(name))
        {
            return SindriErrors.InvalidInput.With($"'{name}' is not a packet file name (fix: a plain name such as scope-map.json, no path)");
        }

        var home = Path.GetFullPath(settings.Home).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var folder = Path.IsPathRooted(directory) ? Path.GetFullPath(directory) : string.Empty;
        if (!folder.StartsWith(home, StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"'{directory}' is not a directory inside Sindri's home (fix: a packet directory is under $AW_SINDRI_HOME)");
        }

        var path = Path.Combine(folder, name);
        if (!File.Exists(path) || new FileInfo(path).Length > MaxBytes)
        {
            return SindriErrors.ItemNotFound.With($"The packet in {directory} has no readable file {name}.");
        }

        try
        {
            return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ItemNotFound.With($"The packet file {name} in {directory} cannot be read: {e.Message}");   // boundary: disk
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]*(\\.[A-Za-z0-9_-]+)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FileName { get; }
}
