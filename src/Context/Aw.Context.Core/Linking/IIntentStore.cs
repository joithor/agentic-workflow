using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Linking;

/// <summary>The intent layer's store (spec §6.2, S-5B): what syncs wrote, outside the map versions, so a build or promote never drops it.</summary>
public interface IIntentStore
{
    /// <summary>Everything stored, as <see cref="IntentOverlay"/> merges it.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The layer (empty before any sync), or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IntentLayer>> Layer(CancellationToken cancellationToken);

    /// <summary>One source's sync state.</summary>
    /// <param name="source">The source.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The state (zeros before any sync), or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IntentSourceState>> State(SourceId source, CancellationToken cancellationToken);

    /// <summary>In one transaction stores the page's records and links and moves the source's cursor; a failure changes nothing, cursor included. A replay of the same page writes no new rows.</summary>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>What it did, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<IntentCommit>> CommitPage(IntentPage page, CancellationToken cancellationToken);

    /// <summary>Records that a run of the source failed, keeping its cursor and last sync.</summary>
    /// <param name="source">The source.</param>
    /// <param name="code">The error code.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or SND-CONTEXT-004.</returns>
    public ValueTask<Result<Unit>> RecordFailure(SourceId source, string code, CancellationToken cancellationToken);
}
