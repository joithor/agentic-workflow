using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Context.Infrastructure.State;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Map;

/// <summary>The exports folder: JSON files written by temp file and rename at 0600 in a 0700 folder, nowhere else.</summary>
internal sealed class FileExportStore(ContextSettings settings, JsonSerializerOptions options) : IExportStore
{
    public ValueTask<Result<string>> Write(string? path, string defaultName, MapDocument document, CancellationToken cancellationToken)
    {
        var folder = Path.GetFullPath(settings.ExportsDirectory);
        var target = Path.GetFullPath(path ?? defaultName, folder);
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
            OwnerOnly.EnsureDirectory(Path.GetDirectoryName(folder)!);   // why: folder is an absolute path under the state directory
            OwnerOnly.EnsureDirectory(folder);
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
        var folder = Path.GetFullPath(settings.ExportsDirectory);
        if (!Directory.Exists(folder))
        {
            return Done(Results.Success<IReadOnlyList<ExportedFile>>([]));
        }

        IReadOnlyList<ExportedFile> files = [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Select(f => new ExportedFile(f, new DateTimeOffset(File.GetLastWriteTimeUtc(f), TimeSpan.Zero)))
            .OrderBy(f => f.Path, StringComparer.Ordinal)];
        return Done(Results.Success(files));
    }

    public ValueTask<Result<bool>> Delete(string path, CancellationToken cancellationToken)
    {
        var full = Path.GetFullPath(path);
        if (!Inside(Path.GetFullPath(settings.ExportsDirectory), full) || !File.Exists(full))
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

    private static bool Inside(string folder, string path)
    {
        var relative = Path.GetRelativePath(folder, path);
        return relative != "." && !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);
}
