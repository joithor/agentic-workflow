using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Map;

/// <summary>The exports folder: JSON files written by temp file and rename at 0600 in a 0700 folder, nowhere else.</summary>
internal sealed class FileExportStore(ContextStoreSettings settings, JsonSerializerOptions options) : IExportStore
{
    public ValueTask<Result<string>> Write(string? path, string defaultName, MapDocument document, CancellationToken cancellationToken)
    {
        var lexical = Path.GetFullPath(settings.ExportsDirectory);
        var folder = RealPath.Of(lexical) ?? lexical;
        var full = Path.GetFullPath(string.IsNullOrEmpty(path) ? defaultName : path, lexical);
        var target = RealPath.Of(full) ?? full;
        if (Worktrees.Containing(target) is { } worktree)
        {
            return Done<string>(ContextErrors.PathInWorktree.With($"{target} is inside the git worktree {worktree} (fix: omit the path, or give a name under {folder})"));
        }

        if (!Inside(folder, target))
        {
            return Done<string>(ContextErrors.ExportOutsideHome.With($"{target} is outside {folder} (fix: omit the path, or give a name under it)"));
        }

        try
        {
            OwnerOnly.EnsureDirectory(settings.Home);
            OwnerOnly.EnsureDirectory(lexical);
            OwnerOnly.EnsureDirectory(Path.GetDirectoryName(target)!);   // why: target is an absolute file path inside folder
            OwnerOnly.WriteAtomically(target, AwJson.Serialize(document, options));
            return Done(Results.Success(target));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Done<string>(ContextErrors.StoreUnavailable.With($"{target} can't be written: {e.Message}"));
        }
    }

    public ValueTask<Result<IReadOnlyList<ExportedFile>>> List(CancellationToken cancellationToken)
    {
        var folder = Folder();
        if (!Directory.Exists(folder))
        {
            return Done(Results.Success<IReadOnlyList<ExportedFile>>([]));
        }

        try
        {
            IReadOnlyList<ExportedFile> files = [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Select(f => new ExportedFile(f, new DateTimeOffset(File.GetLastWriteTimeUtc(f), TimeSpan.Zero)))
                .OrderBy(f => f.Path, StringComparer.Ordinal)];
            return Done(Results.Success(files));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Done<IReadOnlyList<ExportedFile>>(ContextErrors.StoreUnavailable.With($"{folder} can't be listed: {e.Message}"));
        }
    }

    public ValueTask<Result<bool>> Delete(string path, CancellationToken cancellationToken)
    {
        var full = RealPath.Of(path) ?? Path.GetFullPath(path);
        if (!Inside(Folder(), full) || !File.Exists(full))
        {
            return Done(Results.Success(value: false));
        }

        try
        {
            File.Delete(full);
            return Done(Results.Success(value: true));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Done<bool>(ContextErrors.StoreUnavailable.With($"{full} can't be deleted: {e.Message}"));
        }
    }

    /// <summary>The exports folder where it really is (links resolved), as Write returns paths.</summary>
    private string Folder()
    {
        var lexical = Path.GetFullPath(settings.ExportsDirectory);
        return RealPath.Of(lexical) ?? lexical;
    }

    private static bool Inside(string folder, string path)
    {
        var relative = Path.GetRelativePath(folder, path);
        var first = relative.Split(Path.DirectorySeparatorChar)[0];
        return relative != "." && first != ".." && !Path.IsPathRooted(relative);
    }

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);
}
