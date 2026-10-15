using Aw.Context.Core;
using Aw.Context.Core.Review;
using Aw.Kernel;

namespace Aw.Context.Testing.Review;

/// <summary>An in-memory review file store rooted at <see cref="Folder"/>; <see cref="ReviewFileStoreContract"/> holds it to the file store's behavior.</summary>
public sealed class FakeReviewFileStore : IReviewFileStore
{
    private readonly Dictionary<string, ReviewFile> _files = new(StringComparer.Ordinal);

    /// <summary>The folder every review file is written to.</summary>
    public const string Folder = "/home/sindri/context/exports";

    /// <summary>A path the fake treats as inside a git worktree.</summary>
    public const string WorktreeRoot = "/work/repo";

    /// <summary>The files written, by path.</summary>
    public IReadOnlyDictionary<string, ReviewFile> Files => _files;

    /// <inheritdoc/>
    public ValueTask<Result<string>> Write(string? path, string defaultName, ReviewFile file, CancellationToken cancellationToken)
    {
        var target = Path.GetFullPath(path ?? defaultName, Folder);
        if (target.StartsWith(WorktreeRoot + "/", StringComparison.Ordinal))
        {
            return ValueTask.FromResult<Result<string>>(ContextErrors.PathInWorktree.With($"{target} is inside the git worktree {WorktreeRoot}"));
        }

        if (!target.StartsWith(Folder + "/", StringComparison.Ordinal))
        {
            return ValueTask.FromResult<Result<string>>(ContextErrors.ExportOutsideHome.With($"{target} is outside {Folder}"));
        }

        _files[target] = file;
        return ValueTask.FromResult<Result<string>>(target);
    }

    /// <inheritdoc/>
    public ValueTask<Result<ReviewFile>> Read(string path, CancellationToken cancellationToken)
    {
        if (path.StartsWith(WorktreeRoot + "/", StringComparison.Ordinal))
        {
            return ValueTask.FromResult<Result<ReviewFile>>(ContextErrors.PathInWorktree.With($"{path} is inside the git worktree {WorktreeRoot}"));
        }

        return ValueTask.FromResult<Result<ReviewFile>>(_files.TryGetValue(path, out var file) ? file : ContextErrors.MapFileInvalid.With($"{path} can't be read"));
    }
}
