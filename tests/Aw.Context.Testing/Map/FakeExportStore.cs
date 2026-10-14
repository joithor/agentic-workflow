using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Testing.Map;

/// <summary>An in-memory exports folder rooted at <see cref="Folder"/>; <see cref="ExportStoreContract"/> holds it to the file store's behavior.</summary>
/// <param name="clock">Stamps each write.</param>
public sealed class FakeExportStore(TimeProvider clock) : IExportStore
{
    private readonly Dictionary<string, (MapDocument Document, DateTimeOffset Written)> _files = new(StringComparer.Ordinal);

    /// <summary>The folder every export lives in.</summary>
    public const string Folder = "/home/sindri/context/exports";

    /// <summary>A path the fake treats as inside a git worktree.</summary>
    public const string WorktreeRoot = "/work/repo";

    /// <summary>The documents written, by path.</summary>
    public IReadOnlyDictionary<string, MapDocument> Documents => _files.ToDictionary(f => f.Key, f => f.Value.Document, StringComparer.Ordinal);

    /// <summary>Backdates a file.</summary>
    /// <param name="path">A written path.</param>
    /// <param name="written">Its new write time.</param>
    public void Touch(string path, DateTimeOffset written) => _files[path] = (_files[path].Document, written);

    /// <inheritdoc/>
    public ValueTask<Result<string>> Write(string? path, string defaultName, MapDocument document, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Place(Path.GetFullPath(path ?? defaultName, Folder), document));

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ExportedFile>>> List(CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyList<ExportedFile>>([.. _files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => new ExportedFile(f.Key, f.Value.Written))]));

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Delete(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(_files.Remove(path)));

    private Result<string> Place(string target, MapDocument document)
    {
        if (target.StartsWith(WorktreeRoot + "/", StringComparison.Ordinal))
        {
            return ContextErrors.PathInWorktree.With($"{target} is inside the git worktree {WorktreeRoot}");
        }

        if (!target.StartsWith(Folder + "/", StringComparison.Ordinal))
        {
            return ContextErrors.ExportOutsideHome.With($"{target} is outside {Folder}");
        }

        _files[target] = (document, clock.GetUtcNow());
        return target;
    }
}
