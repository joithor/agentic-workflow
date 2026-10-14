using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>The exports folder, $AW_SINDRI_HOME/context/exports/ (0700, files 0600 by temp file and rename).</summary>
public interface IExportStore
{
    /// <summary>Writes <paramref name="document"/> as JSON.</summary>
    /// <param name="path">A file name or a path under the exports folder, or null for a name of the store's choosing.</param>
    /// <param name="defaultName">The name to use when <paramref name="path"/> is null.</param>
    /// <param name="document">The map.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The absolute path written, SND-CONTEXT-005 inside a worktree, or SND-CONTEXT-033 outside the exports folder.</returns>
    public ValueTask<Result<string>> Write(string? path, string defaultName, MapDocument document, CancellationToken cancellationToken);

    /// <summary>The files in the exports folder.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The files (none when the folder doesn't exist).</returns>
    public ValueTask<Result<IReadOnlyList<ExportedFile>>> List(CancellationToken cancellationToken);

    /// <summary>Deletes one file of the exports folder.</summary>
    /// <param name="path">A path <see cref="List"/> returned.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether a file was deleted.</returns>
    public ValueTask<Result<bool>> Delete(string path, CancellationToken cancellationToken);
}
