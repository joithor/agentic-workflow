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

    /// <summary>A retained or current version with its graph and every override, read in one session (scoping spec C10).</summary>
    /// <param name="version">The version number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The snapshot, null when no version with that number is 'current' or 'retained' (never written, purged, a draft, still building), or SND-CONTEXT-004.</returns>
    public ValueTask<Result<MapSnapshot?>> SnapshotAt(int version, CancellationToken cancellationToken);

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

    /// <summary>
    /// Replaces the open draft <paramref name="draft"/> with <paramref name="map"/> as a new version in batches ('building'), then in one short transaction makes it the draft. Unlike <see cref="SaveDraft"/>
    /// it sweeps no other 'building' version and deletes nothing but <paramref name="draft"/>: if another draft appeared or the draft was promoted or replaced meanwhile, nothing changes.
    /// </summary>
    /// <param name="draft">The draft's version number, as read.</param>
    /// <param name="map">The checked map (its overrides join the layer; one already there is kept once).</param>
    /// <param name="created">The time to record.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new draft version, SND-CONTEXT-046 when <paramref name="draft"/> is no longer the open draft (it changed; read it again), or SND-CONTEXT-004 (no draft appears then).</returns>
    public ValueTask<Result<MapVersion>> ReplaceDraft(int draft, MapImport map, DateTimeOffset created, CancellationToken cancellationToken);

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
    /// <param name="expectedDraft">When given, the rows are appended only while that is the latest draft (checked in the same transaction: the layer is shared across versions, so a person's build during a cleanup pass must not receive the old draft's rows).</param>
    /// <returns>How many rows were added, SND-CONTEXT-190 when <paramref name="expectedDraft"/> is not the latest draft, or SND-CONTEXT-004 (nothing is added then).</returns>
    public ValueTask<Result<int>> AppendOverrides(IReadOnlyList<MapOverride> overrides, CancellationToken cancellationToken, int? expectedDraft = null);

    /// <summary>The commits a version was built at (map_version_head), in the order the build named them.</summary>
    /// <param name="version">The version.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The heads (empty for a version built before heads were recorded, or one that is not there), or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IReadOnlyList<MapHead>>> Heads(int version, CancellationToken cancellationToken);

    /// <summary>The hold, the latest draft, the current version and its heads, read together.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The state, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<MapPublishState>> Held(CancellationToken cancellationToken);

    /// <summary>
    /// In one transaction: checks that <paramref name="hold"/> names the latest draft, takes the start of the wait from the chain row
    /// (<see cref="MapHold.NextSince"/> over the stored one and <see cref="MapHold.Since"/> as now; the chain is written when there is none) and stores the hold,
    /// replacing any other.
    /// </summary>
    /// <param name="hold">The draft and the time now as its <see cref="MapHold.Since"/>; its heads are checked, and the stored ones are the version's own rows.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stored hold, SND-CONTEXT-190 when the draft is not the latest (nothing changes), 191 for an invalid head, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<MapHold>> Hold(MapHold hold, CancellationToken cancellationToken);

    /// <summary>Removes the hold on a draft (the chain stays).</summary>
    /// <param name="version">The draft.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit, also when that draft was not held; or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> Release(int version, CancellationToken cancellationToken);
}
