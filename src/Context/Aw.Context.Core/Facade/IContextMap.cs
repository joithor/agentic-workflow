using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>
/// The map part of the facade (spec Phase 3, stage S-3a): import, export, status, the doctor checks and the purge.
/// IContextEngine extends it; Sindri calls these through IContextEngine only. Reads take the same ContextScope as the rest of the engine.
/// </summary>
public interface IContextMap
{
    /// <summary>Reads and checks a map file without writing anything: what 'context map import' shows before asking for the hash.</summary>
    /// <param name="path">The file.</param>
    /// <param name="scope">The profile's context section and repos: the file's repo must be one the profile maps.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The preview, or SND-CONTEXT-001/005/031/032.</returns>
    public ValueTask<Result<MapFilePreview>> PreviewMap(string path, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Imports a map file as the current version (a protected command: Sindri checks the caller first).</summary>
    /// <param name="path">The file.</param>
    /// <param name="confirmedHash">The content hash the owner confirmed.</param>
    /// <param name="scope">The profile's context section and repos: the file's repo must be one the profile maps.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new version, or SND-CONTEXT-001/004/005/031/032/034 with nothing written.</returns>
    public ValueTask<Result<MapImported>> ImportMap(string path, string confirmedHash, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Exports the current version (with the override layer) to the exports folder.</summary>
    /// <param name="path">A name or path under the exports folder, or null.</param>
    /// <param name="scope">The profile's context section and repos, and the caller: a session's export holds only its repo's scope.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The file, or SND-CONTEXT-001/003/005/020/030/033.</returns>
    public ValueTask<Result<MapExported>> ExportMap(string? path, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The current version and its counts.</summary>
    /// <param name="scope">The profile's context section and repos, and the caller: a session sees its repo's scope only.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The status, or SND-CONTEXT-001/003/020/030.</returns>
    public ValueTask<Result<MapStatusView>> MapStatus(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Doctor 'context:map': whether there is a current map and whether overrides are orphaned.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The check.</returns>
    public ValueTask<Result<MapHealth>> CheckMap(CancellationToken cancellationToken);

    /// <summary>Doctor 'context:retention': expired exports and retained versions the purge should have removed.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The check.</returns>
    public ValueTask<Result<MapHealth>> CheckRetention(CancellationToken cancellationToken);

    /// <summary>The 'context-purge' service's work: deletes expired exports and retained versions beyond the newest 20.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What was removed.</returns>
    public ValueTask<Result<PurgeReport>> PurgeExpired(CancellationToken cancellationToken);
}
