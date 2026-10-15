using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>The product graph's store (spec §6.2): map versions, their graphs, and the override layer that outlives them.</summary>
public interface IGraphStore
{
    /// <summary>The current version.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>It, null when there is none yet, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<MapVersion?>> Current(CancellationToken cancellationToken);

    /// <summary>Every version that isn't being built, newest first.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The versions, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IReadOnlyList<MapVersion>>> Versions(CancellationToken cancellationToken);

    /// <summary>One version's graph as stored (overrides not applied).</summary>
    /// <param name="version">The version.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The graph, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<GraphSnapshot>> Read(int version, CancellationToken cancellationToken);

    /// <summary>The current version (or the newest draft) with its graph and every override, read in one session so they belong together.</summary>
    /// <param name="draft">True for the newest draft version instead of the current one.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshot, null when there is no such version, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<MapSnapshot?>> Snapshot(bool draft, CancellationToken cancellationToken);

    /// <summary>Every override, oldest first.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The overrides, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IReadOnlyList<MapOverride>>> Overrides(CancellationToken cancellationToken);

    /// <summary>
    /// Writes <paramref name="map"/> as a new version in batches ('building'), then in one short transaction makes it
    /// 'current' and the previous current 'retained'. Its overrides join the layer (an override already there is kept once).
    /// </summary>
    /// <param name="map">The checked map.</param>
    /// <param name="created">The time to record.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new version, or SND-CONTEXT-004 (nothing becomes current then).</returns>
    public ValueTask<Result<MapVersion>> SaveCurrent(MapImport map, DateTimeOffset created, CancellationToken cancellationToken);

    /// <summary>Deletes retained versions and their graphs.</summary>
    /// <param name="versions">The versions; a current or draft one is never deleted.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many were deleted, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<int>> Delete(IReadOnlyList<int> versions, CancellationToken cancellationToken);

    /// <summary>
    /// Writes <paramref name="map"/> as a new version in batches ('building'), then in one short transaction makes it the
    /// newest 'draft'; 'current' is untouched. Its overrides join the layer (an override already there is kept once).
    /// </summary>
    /// <param name="map">The checked map (an extraction with the layer's overrides applied or not; the layer is re-applied on every read).</param>
    /// <param name="created">The time to record.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new draft version, or SND-CONTEXT-004 (no draft appears then).</returns>
    public ValueTask<Result<MapVersion>> SaveDraft(MapImport map, DateTimeOffset created, CancellationToken cancellationToken);

    /// <summary>The newest draft, opening one as a copy of 'current' when there is none (a scoping run's proposals need a draft to land in).</summary>
    /// <param name="created">The time to record when a draft is opened.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The draft version, SND-CONTEXT-030 when there is no current version to copy, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<MapVersion>> OpenDraft(DateTimeOffset created, CancellationToken cancellationToken);

    /// <summary>In one transaction makes a draft 'current' and the previous current 'retained'.</summary>
    /// <param name="draft">The draft's version number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new current version, SND-CONTEXT-046 when that version isn't a draft, or SND-CONTEXT-004 (nothing changes then).</returns>
    public ValueTask<Result<MapVersion>> Promote(int draft, CancellationToken cancellationToken);

    /// <summary>Adds overrides to the layer; one whose id is already there is kept once. Each is one row.</summary>
    /// <param name="overrides">The overrides.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many rows were added, or SND-CONTEXT-004 (nothing is added then).</returns>
    public ValueTask<Result<int>> AppendOverrides(IReadOnlyList<MapOverride> overrides, CancellationToken cancellationToken);
}
