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
        var resolved = ExportTarget.Resolve(settings, path, defaultName);
        if (resolved.IsError)
        {
            return Done<string>(resolved.Error);
        }

        var target = resolved.Value;
        return Done(WriteFile(target, document));
    }

    private Result<string> WriteFile(string target, MapDocument document)
    {
        var lexical = Path.GetFullPath(settings.ExportsDirectory);
        try
        {
            OwnerOnly.EnsureDirectory(settings.Home);
            OwnerOnly.EnsureDirectory(lexical);
            OwnerOnly.EnsureDirectory(Path.GetDirectoryName(target)!);   // why: target is an absolute file path inside folder
            OwnerOnly.WriteAtomically(target, AwJson.Serialize(document, options));
            return Results.Success(target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.StoreUnavailable.With($"{target} can't be written: {e.Message}");
        }
    }

    public ValueTask<Result<IReadOnlyList<ExportedFile>>> List(CancellationToken cancellationToken)
    {
        var folder = Folder();
        if (!Directory.Exists(folder))
        {
            return Done(Results.Success<IReadOnlyList<ExportedFile>>([]));
        }

        return Done(Enumerate(folder));
    }

    private static Result<IReadOnlyList<ExportedFile>> Enumerate(string folder)
    {
        try
        {
            IReadOnlyList<ExportedFile> files = [.. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Select(f => new ExportedFile(f, new DateTimeOffset(File.GetLastWriteTimeUtc(f), TimeSpan.Zero)))
                .OrderBy(f => f.Path, StringComparer.Ordinal)];
            return Results.Success(files);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.StoreUnavailable.With($"{folder} can't be listed: {e.Message}");
        }
    }

    public ValueTask<Result<bool>> Delete(string path, CancellationToken cancellationToken)
    {
        var full = RealPath.Of(path) ?? Path.GetFullPath(path);
        if (!Inside(Folder(), full) || !File.Exists(full))
        {
            return Done(Results.Success(value: false));
        }

        return Done(DeleteFile(full));
    }

    private static Result<bool> DeleteFile(string full)
    {
        try
        {
            File.Delete(full);
            return Results.Success(value: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.StoreUnavailable.With($"{full} can't be deleted: {e.Message}");
        }
    }

    /// <summary>The exports folder where it really is (links resolved), as Write returns paths.</summary>
    private string Folder() => ExportTarget.Folder(settings);

    private static bool Inside(string folder, string path) => ExportTarget.Inside(folder, path);

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);
}
