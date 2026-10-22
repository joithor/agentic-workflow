using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary>
/// <see cref="IOutsidePackFiles"/> over the disk: every regular file under '&lt;packs root&gt;/lenses/&lt;id&gt;', read as UTF-8. A link is not followed, so no file from outside
/// the pack's directory can be read through it, and an id that is not a pack id (a path, say) finds nothing.
/// </summary>
internal sealed class DiskPackFiles : IOutsidePackFiles
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string>? Files(string packsRoot, string id)
    {
        ArgumentNullException.ThrowIfNull(packsRoot);
        ArgumentNullException.ThrowIfNull(id);
        var directory = Path.Combine(packsRoot, "lenses", id);
        if (!LensPackId.TryFrom(id, out _) || !Directory.Exists(directory))
        {
            return null;
        }

        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*", options))
        {
            files[Path.GetRelativePath(directory, path).Replace('\\', '/')] = File.ReadAllText(path);
        }

        return files.Count == 0 ? null : files;
    }
}
