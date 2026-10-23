using Aw.Kernel;

namespace Aw.Sindri.Core.EngineReads;

/// <summary>
/// The read side of the Temporal engine in depth (port; API spec §3.8, E-30; ARCH-CQRS-002): the server, the worker, the running instances and one
/// instance's history, as the Temporal client reports them. Its adapter is the only code besides the worker that holds a Temporal client. Every call
/// fails with SND-ENGINE-001 when the server doesn't answer.
/// </summary>
public interface IEngineReader
{
    /// <summary>The Temporal server's facts.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The server, or SND-ENGINE-001.</returns>
    public ValueTask<Result<EngineServer>> Server(CancellationToken cancellationToken);

    /// <summary>The worker's facts, from the task queue's description.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The worker, or SND-ENGINE-001.</returns>
    public ValueTask<Result<EngineWorker>> Worker(CancellationToken cancellationToken);

    /// <summary>How far behind the ledger side of the engine is.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lag, or SND-ENGINE-001.</returns>
    public ValueTask<Result<EngineMirror>> Mirror(CancellationToken cancellationToken);

    /// <summary>Every running recipe instance, by address.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The instances, or SND-ENGINE-001.</returns>
    public ValueTask<Result<IReadOnlyList<EngineInstanceFacts>>> Instances(CancellationToken cancellationToken);

    /// <summary>One page of an instance's history, newest first.</summary>
    /// <param name="reference">The instance: its 'WFI-' id or its address.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The events on a page.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page, SND-ENGINE-404 when Temporal has no execution for the reference, or SND-ENGINE-001.</returns>
    public ValueTask<Result<EngineHistoryFacts>> History(string reference, int page, int pageSize, CancellationToken cancellationToken);
}
